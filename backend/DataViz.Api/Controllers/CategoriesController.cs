using DataViz.Api.Data;
using DataViz.Api.Dtos;
using DataViz.Api.Infrastructure;
using DataViz.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

/// <summary>CRUD категорий товаров. Чтение — публично, запись — только админам.</summary>
[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    public CategoriesController(ApplicationDbContext ctx) => _ctx = ctx;

    /// <summary>Возвращает все категории, отсортированные по имени.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> List(CancellationToken ct) =>
        await _ctx.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description))
            .ToListAsync(ct);

    /// <summary>Возвращает одну категорию по идентификатору.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetOne(int id, CancellationToken ct)
    {
        var c = await _ctx.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? NotFound() : new CategoryDto(c.Id, c.Name, c.Description);
    }

    /// <summary>Создаёт новую категорию. Требует роль admin.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CategoryCreateDto dto, CancellationToken ct)
    {
        var name = dto.Name.Trim();
        if (await _ctx.Categories.AnyAsync(c => c.Name == name, ct))
            return Conflict(new ProblemDetails
            {
                Title = "Category already exists",
                Status = StatusCodes.Status409Conflict,
            });

        var category = new Category { Name = name, Description = dto.Description?.Trim() };
        _ctx.Categories.Add(category);
        await _ctx.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetOne), new { id = category.Id },
            new CategoryDto(category.Id, category.Name, category.Description));
    }

    /// <summary>Обновляет категорию. Требует роль admin.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> Update(int id, [FromBody] CategoryCreateDto dto, CancellationToken ct)
    {
        var c = await _ctx.Categories.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        c.Name = dto.Name.Trim();
        c.Description = dto.Description?.Trim();
        await _ctx.SaveChangesAsync(ct);
        return new CategoryDto(c.Id, c.Name, c.Description);
    }

    /// <summary>Удаляет пустую категорию. Требует роль admin.</summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var category = await _ctx.Categories
            .Include(x => x.Products)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (category is null) return NotFound();
        if (category.Products.Any())
            return BadRequest(new ProblemDetails
            {
                Title = "Category is not empty",
                Detail = "Cannot delete a category that still has products.",
                Status = StatusCodes.Status400BadRequest,
            });
        _ctx.Categories.Remove(category);
        await _ctx.SaveChangesAsync(ct);
        return NoContent();
    }
}
