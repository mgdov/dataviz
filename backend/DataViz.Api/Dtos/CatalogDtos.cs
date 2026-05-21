using System.ComponentModel.DataAnnotations;

namespace DataViz.Api.Dtos;

public record CategoryDto(int Id, string Name, string? Description);

public record CategoryCreateDto(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [StringLength(500)] string? Description);

public record ProductDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    int StockQuantity,
    string RegionCode,
    int CategoryId,
    string? CategoryName);

public record ProductCreateDto(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    [StringLength(1000)] string? Description,
    [Range(0, 1_000_000_000)] decimal Price,
    [Range(0, 1_000_000)] int StockQuantity,
    [Required, StringLength(8)] string RegionCode,
    [Range(1, int.MaxValue)] int CategoryId);
