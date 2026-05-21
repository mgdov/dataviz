using System.Security.Claims;
using DataViz.Api.Data;
using DataViz.Api.Dtos;
using DataViz.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    public OrdersController(ApplicationDbContext ctx) => _ctx = ctx;

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> List()
    {
        var uid = CurrentUserId;
        var orders = await _ctx.Orders
            .Where(o => o.UserId == uid)
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.User)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
        return orders.Select(Map).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> GetOne(int id)
    {
        var o = await _ctx.Orders
            .Include(o => o.Items).ThenInclude(i => i.Product)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (o is null) return NotFound();
        if (o.UserId != CurrentUserId) return Forbid();
        return Map(o);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] OrderCreateDto dto)
    {
        var ids = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _ctx.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var order = new Order
        {
            UserId = CurrentUserId,
            CreatedAt = DateTime.UtcNow,
            RegionCode = dto.RegionCode.Trim().ToUpperInvariant(),
        };
        decimal total = 0;

        foreach (var item in dto.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                return BadRequest(new { error = $"Product {item.ProductId} not found" });
            if (product.StockQuantity < item.Quantity)
                return BadRequest(new { error = $"Not enough stock for product {product.Id}" });

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
        await _ctx.SaveChangesAsync();

        await _ctx.Entry(order).Collection(o => o.Items).LoadAsync();
        foreach (var i in order.Items)
            await _ctx.Entry(i).Reference(x => x.Product).LoadAsync();
        await _ctx.Entry(order).Reference(o => o.User).LoadAsync();

        return CreatedAtAction(nameof(GetOne), new { id = order.Id }, Map(order));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var o = await _ctx.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound();
        if (o.UserId != CurrentUserId) return Forbid();

        // Возврат остатков на склад
        var ids = o.Items.Select(i => i.ProductId).ToList();
        var products = await _ctx.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var item in o.Items)
            if (products.TryGetValue(item.ProductId, out var p))
                p.StockQuantity += item.Quantity;

        _ctx.Orders.Remove(o);
        await _ctx.SaveChangesAsync();
        return NoContent();
    }

    private static OrderDto Map(Order o) => new(
        o.Id, o.UserId, o.User?.Name, o.CreatedAt, o.TotalPrice, o.RegionCode,
        o.Items.Select(i => new OrderItemDto(
            i.ProductId, i.Product?.Name ?? "", i.Quantity, i.UnitPrice)).ToList());
}
