using System.ComponentModel.DataAnnotations;

namespace DataViz.Api.Dtos;

public record RegisterDto(
    [Required, StringLength(120, MinimumLength = 2)] string Name,
    [Required, EmailAddress] string Email,
    [Required, StringLength(100, MinimumLength = 6)] string Password);

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record UserDto(int Id, string Name, string Email, string Role, DateTime CreatedAt);

public record AuthResponseDto(string Token, DateTime ExpiresAt, UserDto User);
