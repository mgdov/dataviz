using DataViz.Api.Data;
using DataViz.Api.Dtos;
using DataViz.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    public CategoriesController(ApplicationDbContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> List() =>
        await _ctx.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description))
            .ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetOne(int id)
    {
        var c = await _ctx.Categories.FindAsync(id);
        return c is null ? NotFound() : new CategoryDto(c.Id, c.Name, c.Description);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CategoryCreateDto dto)
    {
        var name = dto.Name.Trim();
        if (await _ctx.Categories.AnyAsync(c => c.Name == name))
            return Conflict(new { error = "Category already exists" });

        var c = new Category { Name = name, Description = dto.Description };
        _ctx.Categories.Add(c);
        await _ctx.SaveChangesAsync();
        return CreatedAtAction(nameof(GetOne), new { id = c.Id },
            new CategoryDto(c.Id, c.Name, c.Description));
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CategoryDto>> Update(int id, [FromBody] CategoryCreateDto dto)
    {
        var c = await _ctx.Categories.FindAsync(id);
        if (c is null) return NotFound();
        c.Name = dto.Name.Trim();
        c.Description = dto.Description;
        await _ctx.SaveChangesAsync();
        return new CategoryDto(c.Id, c.Name, c.Description);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _ctx.Categories.Include(x => x.Products).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        if (c.Products.Any()) return BadRequest(new { error = "Category has products" });
        _ctx.Categories.Remove(c);
        await _ctx.SaveChangesAsync();
        return NoContent();
    }
}
