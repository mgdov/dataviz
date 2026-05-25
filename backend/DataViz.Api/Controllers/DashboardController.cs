using DataViz.Api.Data;
using DataViz.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

/// <summary>
/// Поставщик агрегатов для дашборда: KPI, временной ряд выручки,
/// доли категорий, тепловая карта регион×категория, топ-10 товаров.
/// </summary>
[Authorize]
[ApiController]
[Route("api/dashboard")]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ApplicationDbContext ctx, ILogger<DashboardController> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    /// <summary>
    /// Возвращает все агрегаты для дашборда продаж за выбранный период.
    /// Все агрегации выполняются на стороне PostgreSQL, чтобы избежать переноса
    /// больших таблиц в память приложения.
    /// </summary>
    /// <param name="from">Дата начала периода (UTC). По умолчанию — год назад.</param>
    /// <param name="to">Дата конца периода (UTC). По умолчанию — текущая дата.</param>
    /// <param name="regions">Список регионов через запятую (например, <c>MSK,SPB</c>).</param>
    /// <param name="categoryId">Идентификатор категории для фильтрации.</param>
    /// <param name="ct">Токен отмены запроса.</param>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(SalesDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SalesDashboardDto>> Sales(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? regions,
        [FromQuery] int? categoryId,
        CancellationToken ct)
    {
        var fromDt = DateTime.SpecifyKind((from ?? DateTime.UtcNow.AddYears(-1)).Date, DateTimeKind.Utc);
        var toDt = DateTime.SpecifyKind((to ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        if (fromDt > toDt)
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid period",
                Detail = "Parameter 'from' must be earlier than or equal to 'to'.",
                Status = StatusCodes.Status400BadRequest,
            });

        var regionList = string.IsNullOrWhiteSpace(regions)
            ? Array.Empty<string>()
            : regions
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(r => r.ToUpperInvariant())
                .ToArray();

        // Базовый запрос: OrderItem с фильтрами по дате/региону/категории.
        // Все агрегаты ниже переводятся в SQL и выполняются на стороне PostgreSQL.
        // Сложные подзапросы (Count Distinct внутри GroupBy) EF Core 8 не умеет
        // переводить — для них используем отдельные запросы и мерж в памяти.
        var items = _ctx.OrderItems.AsNoTracking()
            .Where(i => i.Order!.CreatedAt >= fromDt && i.Order!.CreatedAt <= toDt);

        if (regionList.Length > 0)
            items = items.Where(i => regionList.Contains(i.Order!.RegionCode));
        if (categoryId.HasValue)
            items = items.Where(i => i.Product!.CategoryId == categoryId.Value);

        // KPI считаем отдельными лёгкими запросами; каждый — один SQL round-trip.
        var revenue = await items.SumAsync(i => (decimal?)(i.Quantity * i.UnitPrice), ct) ?? 0m;

        var perOrder = await items
            .GroupBy(i => i.OrderId)
            .Select(g => g.Sum(x => x.Quantity * x.UnitPrice))
            .ToListAsync(ct);

        var uniqueCustomers = await items
            .Where(i => i.Order!.UserId != null)
            .Select(i => i.Order!.UserId)
            .Distinct()
            .CountAsync(ct);

        var ordersCount = perOrder.Count;
        var aov = ordersCount > 0 ? perOrder.Average() : 0m;
        var kpi = new KpiDto(revenue, ordersCount, aov, uniqueCustomers);

        // Временной ряд: revenue per day напрямую, orders per day — distinct по (day, orderId).
        var revenuePerDay = await items
            .GroupBy(i => i.Order!.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(x => x.Quantity * x.UnitPrice) })
            .ToListAsync(ct);
        var ordersPerDay = await items
            .Select(i => new { Date = i.Order!.CreatedAt.Date, i.OrderId })
            .Distinct()
            .GroupBy(x => x.Date)
            .Select(g => new { Date = g.Key, Orders = g.Count() })
            .ToListAsync(ct);
        var ordersPerDayMap = ordersPerDay.ToDictionary(x => x.Date, x => x.Orders);
        var timeseries = revenuePerDay
            .OrderBy(x => x.Date)
            .Select(x => new SeriesPointDto(
                x.Date,
                x.Revenue,
                ordersPerDayMap.TryGetValue(x.Date, out var c) ? c : 0))
            .ToList();

        // Доли категорий: revenue per category + distinct orders per category.
        var revenuePerCategory = await items
            .GroupBy(i => i.Product!.Category!.Name)
            .Select(g => new { Category = g.Key, Revenue = g.Sum(x => x.Quantity * x.UnitPrice) })
            .ToListAsync(ct);
        var ordersPerCategory = await items
            .Select(i => new { Category = i.Product!.Category!.Name, i.OrderId })
            .Distinct()
            .GroupBy(x => x.Category)
            .Select(g => new { Category = g.Key, Orders = g.Count() })
            .ToListAsync(ct);
        var ordersPerCategoryMap = ordersPerCategory.ToDictionary(x => x.Category, x => x.Orders);
        var categoryShare = revenuePerCategory
            .OrderByDescending(x => x.Revenue)
            .Select(x => new CategoryShareDto(
                x.Category,
                x.Revenue,
                ordersPerCategoryMap.TryGetValue(x.Category, out var c) ? c : 0))
            .ToList();

        // Тепловая карта регион × категория — только Sum, без Distinct Count.
        var regionCategoryRaw = await items
            .GroupBy(i => new
            {
                Region = i.Order!.RegionCode,
                Category = i.Product!.Category!.Name,
            })
            .Select(g => new
            {
                g.Key.Region,
                g.Key.Category,
                Revenue = g.Sum(x => x.Quantity * x.UnitPrice),
            })
            .ToListAsync(ct);
        var regionCategory = regionCategoryRaw
            .OrderBy(x => x.Region)
            .ThenBy(x => x.Category)
            .Select(x => new RegionCategoryPointDto(x.Region, x.Category, x.Revenue))
            .ToList();

        // Топ-10 товаров — Sum по выручке и количеству.
        var topProductsRaw = await items
            .GroupBy(i => new
            {
                ProductId = i.Product!.Id,
                ProductName = i.Product!.Name,
                Category = i.Product!.Category!.Name,
            })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.Category,
                Revenue = g.Sum(x => x.Quantity * x.UnitPrice),
                Units = g.Sum(x => x.Quantity),
            })
            .OrderByDescending(p => p.Revenue)
            .Take(10)
            .ToListAsync(ct);
        var topProducts = topProductsRaw
            .Select(p => new TopProductDto(p.ProductId, p.ProductName, p.Category, p.Revenue, p.Units))
            .ToList();

        _logger.LogDebug(
            "Dashboard sales aggregated: {Orders} orders, {Revenue} revenue from {From} to {To}",
            ordersCount, revenue, fromDt, toDt);

        return new SalesDashboardDto(kpi, timeseries, categoryShare, regionCategory, topProducts);
    }
}
