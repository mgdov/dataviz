using System.ComponentModel.DataAnnotations;

namespace DataViz.Api.Dtos;

public record OrderItemCreateDto(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, 1000)] int Quantity);

public record OrderCreateDto(
    [Required, StringLength(8)] string RegionCode,
    [Required, MinLength(1)] List<OrderItemCreateDto> Items);

public record OrderItemDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice);

public record OrderDto(
    int Id,
    int? UserId,
    string? UserName,
    DateTime CreatedAt,
    decimal TotalPrice,
    string RegionCode,
    List<OrderItemDto> Items);
