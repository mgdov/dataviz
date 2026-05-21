using DataViz.Api.Models;

namespace DataViz.Api.Auth;

public interface IJwtTokenService
{
    (string token, DateTime expiresAt) CreateToken(User user);
}
