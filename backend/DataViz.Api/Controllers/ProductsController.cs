using DataViz.Api.Data;
using DataViz.Api.Dtos;
using DataViz.Api.Infrastructure;
using DataViz.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

/// <summary>CRUD товаров. Чтение — публично, запись — только админам.</summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public sealed class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    public ProductsController(ApplicationDbContext ctx) => _ctx = ctx;

    /// <summary>Возвращает товары с опциональной фильтрацией по категории.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> List(
        [FromQuery] int? categoryId,
        CancellationToken ct)
    {
        var query = _ctx.Products.AsNoTracking().Where(p => true);
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);

        return await query
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Description, p.Price, p.StockQuantity,
                p.RegionCode, p.CategoryId, p.Category!.Name))
            .ToListAsync(ct);
    }

    /// <summary>Возвращает один товар по идентификатору.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetOne(int id, CancellationToken ct)
    {
        var p = await _ctx.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        return new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity,
            p.RegionCode, p.CategoryId, p.Category!.Name);
    }

    /// <summary>Создаёт товар. Требует роль admin.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create([FromBody] ProductCreateDto dto, CancellationToken ct)
    {
        var cat = await _ctx.Categories.FindAsync(new object[] { dto.CategoryId }, ct);
        if (cat is null)
            return BadRequest(new ProblemDetails
            {
                Title = "Category not found",
                Status = StatusCodes.Status400BadRequest,
            });

        var product = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            RegionCode = dto.RegionCode.Trim().ToUpperInvariant(),
            CategoryId = dto.CategoryId,
        };
        _ctx.Products.Add(product);
        await _ctx.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetOne), new { id = product.Id },
            new ProductDto(product.Id, product.Name, product.Description, product.Price,
                product.StockQuantity, product.RegionCode, product.CategoryId, cat.Name));
    }

    /// <summary>Обновляет товар. Требует роль admin.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Update(int id, [FromBody] ProductCreateDto dto, CancellationToken ct)
    {
        var product = await _ctx.Products
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product is null) return NotFound();

        var cat = await _ctx.Categories.FindAsync(new object[] { dto.CategoryId }, ct);
        if (cat is null)
            return BadRequest(new ProblemDetails
            {
                Title = "Category not found",
                Status = StatusCodes.Status400BadRequest,
            });

        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim();
        product.Price = dto.Price;
        product.StockQuantity = dto.StockQuantity;
        product.RegionCode = dto.RegionCode.Trim().ToUpperInvariant();
        product.CategoryId = dto.CategoryId;
        await _ctx.SaveChangesAsync(ct);

        return new ProductDto(product.Id, product.Name, product.Description, product.Price,
            product.StockQuantity, product.RegionCode, product.CategoryId, cat.Name);
    }

    /// <summary>Удаляет товар без истории заказов. Требует роль admin.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var product = await _ctx.Products
            .Include(x => x.OrderItems)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (product is null) return NotFound();
        if (product.OrderItems.Any())
            return BadRequest(new ProblemDetails
            {
                Title = "Product has order history",
                Detail = "Cannot delete a product that is referenced by existing orders.",
                Status = StatusCodes.Status400BadRequest,
            });
        _ctx.Products.Remove(product);
        await _ctx.SaveChangesAsync(ct);
        return NoContent();
    }
}
