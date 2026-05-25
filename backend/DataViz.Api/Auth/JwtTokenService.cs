using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DataViz.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DataViz.Api.Auth;

/// <summary>
/// Выпускает HS256 JWT-токены для аутентифицированных пользователей.
/// Получает параметры через <see cref="IOptionsMonitor{TOptions}"/>, что позволяет
/// перечитывать конфигурацию без рестарта сервиса.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IOptionsMonitor<JwtOptions> _options;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtTokenService(IOptionsMonitor<JwtOptions> options) => _options = options;

    public (string token, DateTime expiresAt) CreateToken(User user)
    {
        var opt = _options.CurrentValue;
        var expiresAt = DateTime.UtcNow.AddMinutes(opt.ExpireMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: opt.Issuer,
            audience: opt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: creds);

        return (_handler.WriteToken(token), expiresAt);
    }
}
