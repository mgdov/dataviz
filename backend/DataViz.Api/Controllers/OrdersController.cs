using System.Security.Claims;
using DataViz.Api.Data;
using DataViz.Api.Dtos;
using DataViz.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

/// <summary>CRUD заказов для текущего пользователя.</summary>
[Authorize]
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ApplicationDbContext ctx, ILogger<OrdersController> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                  ?? throw new InvalidOperationException("User id claim is missing"));

    /// <summary>Возвращает заказы текущего пользователя, отсортированные от свежих к старым.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrderDto>>> List(CancellationToken ct)
    {
        var uid = CurrentUserId;
        var orders = await _ctx.Orders
            .AsNoTracking()
            .Where(o => o.UserId == uid)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
        return orders.Select(Map).ToList();
    }

    /// <summary>Возвращает один заказ по идентификатору.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOne(int id, CancellationToken ct)
    {
        var order = await _ctx.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return NotFound();
        if (order.UserId != CurrentUserId) return Forbid();
        return Map(order);
    }

    /// <summary>
    /// Создаёт заказ от имени текущего пользователя.
    /// Списание остатков и запись заказа выполняются в одной транзакции — заказ
    /// либо создаётся целиком, либо не создаётся вовсе.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> Create([FromBody] OrderCreateDto dto, CancellationToken ct)
    {
        if (dto.Items.Count == 0)
            return BadRequest(new ProblemDetails
            {
                Title = "Empty order",
                Detail = "Order must contain at least one item.",
                Status = StatusCodes.Status400BadRequest,
            });

        var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToArray();
        var strategy = _ctx.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<ActionResult<OrderDto>>(async () =>
        {
            await using var tx = await _ctx.Database.BeginTransactionAsync(ct);

            var products = await _ctx.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            var order = new Order
            {
                UserId = CurrentUserId,
                CreatedAt = DateTime.UtcNow,
                RegionCode = dto.RegionCode.Trim().ToUpperInvariant(),
            };
            decimal total = 0m;

            foreach (var item in dto.Items)
            {
                if (!products.TryGetValue(item.ProductId, out var product))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Product not found",
                        Detail = $"Product {item.ProductId} does not exist.",
                        Status = StatusCodes.Status400BadRequest,
                    });
                }
                if (product.StockQuantity < item.Quantity)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Insufficient stock",
                        Detail = $"Not enough stock for product {product.Id} ({product.Name}). " +
                                 $"Requested {item.Quantity}, available {product.StockQuantity}.",
                        Status = StatusCodes.Status400BadRequest,
                    });
                }

                product.StockQuantity -= item.Quantity;
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                });
                total += product.Price * item.Quantity;
            }

            order.TotalPrice = total;
            _ctx.Orders.Add(order);
            await _ctx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Order {OrderId} created by user {UserId}, total {Total}",
                order.Id, order.UserId, order.TotalPrice);

            await _ctx.Entry(order).Collection(o => o.Items).LoadAsync(ct);
            foreach (var i in order.Items)
                await _ctx.Entry(i).Reference(x => x.Product).LoadAsync(ct);
            await _ctx.Entry(order).Reference(o => o.User).LoadAsync(ct);

            return CreatedAtAction(nameof(GetOne), new { id = order.Id }, Map(order));
        });
    }

    /// <summary>
    /// Удаляет заказ текущего пользователя и возвращает товары на склад.
    /// Операция выполняется в одной транзакции.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var strategy = _ctx.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            await using var tx = await _ctx.Database.BeginTransactionAsync(ct);

            var order = await _ctx.Orders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (order is null) return NotFound();
            if (order.UserId != CurrentUserId) return Forbid();

            var productIds = order.Items.Select(i => i.ProductId).ToArray();
            var products = await _ctx.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (var item in order.Items)
                if (products.TryGetValue(item.ProductId, out var product))
                    product.StockQuantity += item.Quantity;

            _ctx.Orders.Remove(order);
            await _ctx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("Order {OrderId} deleted by user {UserId}", id, CurrentUserId);
            return NoContent();
        });
    }

    private static OrderDto Map(Order o) => new(
        o.Id,
        o.UserId,
        o.User?.Name,
        o.CreatedAt,
        o.TotalPrice,
        o.RegionCode,
        o.Items
            .Select(i => new OrderItemDto(i.ProductId, i.Product?.Name ?? string.Empty, i.Quantity, i.UnitPrice))
            .ToList());
}
