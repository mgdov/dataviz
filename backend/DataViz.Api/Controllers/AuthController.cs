using System.Security.Claims;
using DataViz.Api.Auth;
using DataViz.Api.Data;
using DataViz.Api.Dtos;
using DataViz.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    private readonly IJwtTokenService _jwt;

    public AuthController(ApplicationDbContext ctx, IJwtTokenService jwt)
    {
        _ctx = ctx;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        var emailNorm = dto.Email.Trim().ToLowerInvariant();
        if (await _ctx.Users.AnyAsync(u => u.Email == emailNorm))
            return Conflict(new { error = "Email already registered" });

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = emailNorm,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        };
        _ctx.Users.Add(user);
        await _ctx.SaveChangesAsync();

        var (token, expires) = _jwt.CreateToken(user);
        return new AuthResponseDto(token, expires,
            new UserDto(user.Id, user.Name, user.Email, user.Role, user.CreatedAt));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var emailNorm = dto.Email.Trim().ToLowerInvariant();
        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Email == emailNorm);
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password" });

        var (token, expires) = _jwt.CreateToken(user);
        return new AuthResponseDto(token, expires,
            new UserDto(user.Id, user.Name, user.Email, user.Role, user.CreatedAt));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return Unauthorized();

        var user = await _ctx.Users.FindAsync(id);
        if (user is null) return Unauthorized();

        return new UserDto(user.Id, user.Name, user.Email, user.Role, user.CreatedAt);
    }
}
