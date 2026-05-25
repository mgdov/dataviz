using System.Security.Claims;
using DataViz.Api.Auth;
using DataViz.Api.Data;
using DataViz.Api.Dtos;
using DataViz.Api.Infrastructure;
using DataViz.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DataViz.Api.Controllers;

/// <summary>Регистрация, логин и профиль текущего пользователя.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _ctx;
    private readonly IJwtTokenService _jwt;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ApplicationDbContext ctx, IJwtTokenService jwt, ILogger<AuthController> logger)
    {
        _ctx = ctx;
        _jwt = jwt;
        _logger = logger;
    }

    /// <summary>Создаёт нового пользователя с ролью <c>user</c> и возвращает JWT-токен.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    {
        var emailNorm = dto.Email.Trim().ToLowerInvariant();
        if (await _ctx.Users.AnyAsync(u => u.Email == emailNorm))
            return Conflict(new ProblemDetails
            {
                Title = "Email already registered",
                Status = StatusCodes.Status409Conflict,
            });

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = emailNorm,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = UserRoles.User,
            CreatedAt = DateTime.UtcNow,
        };
        _ctx.Users.Add(user);
        await _ctx.SaveChangesAsync();

        _logger.LogInformation("User registered: {Email}", emailNorm);

        var (token, expires) = _jwt.CreateToken(user);
        return Ok(new AuthResponseDto(token, expires,
            new UserDto(user.Id, user.Name, user.Email, user.Role, user.CreatedAt)));
    }

    /// <summary>Проверяет учётные данные и возвращает JWT-токен.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth-strict")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        var emailNorm = dto.Email.Trim().ToLowerInvariant();
        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Email == emailNorm);
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            _logger.LogInformation("Failed login attempt for {Email}", emailNorm);
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid email or password",
                Status = StatusCodes.Status401Unauthorized,
            });
        }

        var (token, expires) = _jwt.CreateToken(user);
        return Ok(new AuthResponseDto(token, expires,
            new UserDto(user.Id, user.Name, user.Email, user.Role, user.CreatedAt)));
    }

    /// <summary>Возвращает профиль текущего пользователя по JWT-токену.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(idStr, out var id)) return Unauthorized();

        var user = await _ctx.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return Unauthorized();

        return new UserDto(user.Id, user.Name, user.Email, user.Role, user.CreatedAt);
    }
}
