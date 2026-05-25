using DataViz.Api.Data;
using DataViz.Api.Dtos;
using DataViz.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    public ProductsController(ApplicationDbContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> List([FromQuery] int? categoryId)
    {
        var q = _ctx.Products.Include(p => p.Category).AsQueryable();
        if (categoryId.HasValue) q = q.Where(p => p.CategoryId == categoryId.Value);
        return await q.OrderBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Description, p.Price, p.StockQuantity,
                p.RegionCode, p.CategoryId, p.Category!.Name))
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetOne(int id)
    {
        var p = await _ctx.Products.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        return new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity,
            p.RegionCode, p.CategoryId, p.Category!.Name);
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] ProductCreateDto dto)
    {
        var cat = await _ctx.Categories.FindAsync(dto.CategoryId);
        if (cat is null) return BadRequest(new { error = "Category not found" });

        var p = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            RegionCode = dto.RegionCode.Trim().ToUpperInvariant(),
            CategoryId = dto.CategoryId,
        };
        _ctx.Products.Add(p);
        await _ctx.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOne), new { id = p.Id },
            new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity,
                p.RegionCode, p.CategoryId, cat.Name));
    }

    [Authorize(Roles = "admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductDto>> Update(int id, [FromBody] ProductCreateDto dto)
    {
        var p = await _ctx.Products.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        var cat = await _ctx.Categories.FindAsync(dto.CategoryId);
        if (cat is null) return BadRequest(new { error = "Category not found" });

        p.Name = dto.Name.Trim();
        p.Description = dto.Description;
        p.Price = dto.Price;
        p.StockQuantity = dto.StockQuantity;
        p.RegionCode = dto.RegionCode.Trim().ToUpperInvariant();
        p.CategoryId = dto.CategoryId;
        await _ctx.SaveChangesAsync();

        return new ProductDto(p.Id, p.Name, p.Description, p.Price, p.StockQuantity,
            p.RegionCode, p.CategoryId, cat.Name);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _ctx.Products.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        if (p.OrderItems.Any()) return BadRequest(new { error = "Product has order items" });
        _ctx.Products.Remove(p);
        await _ctx.SaveChangesAsync();
        return NoContent();
    }
}
