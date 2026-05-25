namespace DataViz.Api.Infrastructure;

/// <summary>Имена политик авторизации, используемых в [Authorize(Policy = ...)] атрибутах.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Доступ только для пользователей с ролью <c>admin</c>.</summary>
    public const string AdminOnly = "AdminOnly";
}

/// <summary>Доменные роли пользователей.</summary>
public static class UserRoles
{
    public const string User = "user";
    public const string Admin = "admin";
}
