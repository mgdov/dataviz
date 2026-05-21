using DataViz.Api.Data;
using DataViz.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    public DashboardController(ApplicationDbContext ctx) => _ctx = ctx;

    [HttpGet("sales")]
    public async Task<ActionResult<SalesDashboardDto>> Sales(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? regions,
        [FromQuery] int? categoryId)
    {
        var fromDt = DateTime.SpecifyKind(
            (from ?? DateTime.UtcNow.AddYears(-1)).Date, DateTimeKind.Utc);
        var toDt = DateTime.SpecifyKind(
            (to ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        var regionList = string.IsNullOrWhiteSpace(regions)
            ? new List<string>()
            : regions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(r => r.ToUpperInvariant())
                     .ToList();

        var q = _ctx.OrderItems
            .Include(i => i.Order)
            .Include(i => i.Product).ThenInclude(p => p!.Category)
            .Where(i => i.Order!.CreatedAt >= fromDt && i.Order!.CreatedAt <= toDt);

        if (regionList.Count > 0)
            q = q.Where(i => regionList.Contains(i.Order!.RegionCode));
        if (categoryId.HasValue)
            q = q.Where(i => i.Product!.CategoryId == categoryId.Value);

        var rows = await q.Select(i => new
        {
            i.Order!.Id,
            Date = i.Order!.CreatedAt,
            Region = i.Order!.RegionCode,
            UserId = i.Order!.UserId,
            Category = i.Product!.Category!.Name,
            ProductId = i.Product!.Id,
            ProductName = i.Product!.Name,
            Revenue = i.UnitPrice * i.Quantity,
            i.Quantity,
        }).ToListAsync();

        var kpi = new KpiDto(
            Revenue: rows.Sum(r => r.Revenue),
            OrdersCount: rows.Select(r => r.Id).Distinct().Count(),
            AverageOrderValue: rows.Select(r => new { r.Id, r.Revenue })
                                   .GroupBy(x => x.Id)
                                   .Select(g => g.Sum(x => x.Revenue))
                                   .DefaultIfEmpty(0)
                                   .Average(),
            UniqueCustomers: rows.Where(r => r.UserId.HasValue)
                                 .Select(r => r.UserId!.Value).Distinct().Count());

        var timeseries = rows
            .GroupBy(r => r.Date.Date)
            .OrderBy(g => g.Key)
            .Select(g => new SeriesPointDto(g.Key, g.Sum(x => x.Revenue), g.Select(x => x.Id).Distinct().Count()))
            .ToList();

        var categoryShare = rows
            .GroupBy(r => r.Category)
            .OrderByDescending(g => g.Sum(x => x.Revenue))
            .Select(g => new CategoryShareDto(g.Key, g.Sum(x => x.Revenue), g.Select(x => x.Id).Distinct().Count()))
            .ToList();

        var regionCategory = rows
            .GroupBy(r => new { r.Region, r.Category })
            .Select(g => new RegionCategoryPointDto(g.Key.Region, g.Key.Category, g.Sum(x => x.Revenue)))
            .OrderBy(x => x.Region).ThenBy(x => x.Category)
            .ToList();

        var topProducts = rows
            .GroupBy(r => new { r.ProductId, r.ProductName, r.Category })
            .OrderByDescending(g => g.Sum(x => x.Revenue))
            .Take(10)
            .Select(g => new TopProductDto(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.Category,
                g.Sum(x => x.Revenue),
                g.Sum(x => x.Quantity)))
            .ToList();

        return new SalesDashboardDto(kpi, timeseries, categoryShare, regionCategory, topProducts);
    }
}
