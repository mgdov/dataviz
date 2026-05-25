using System.ComponentModel.DataAnnotations;

namespace DataViz.Api.Auth;

/// <summary>
/// Параметры подписи и валидации JWT-токенов.
/// Загружаются из секции <c>Jwt</c> конфигурации (appsettings/ENV).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Издатель токена (поле <c>iss</c>).</summary>
    [Required, MinLength(1)]
    public string Issuer { get; set; } = "DataViz";

    /// <summary>Аудитория токена (поле <c>aud</c>).</summary>
    [Required, MinLength(1)]
    public string Audience { get; set; } = "DataViz.Web";

    /// <summary>
    /// Симметричный ключ HS256 в виде UTF-8 строки.
    /// Минимальная длина — 32 байта (требование RFC 7518 §3.2).
    /// </summary>
    [Required, MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 bytes long")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Время жизни access-токена в минутах.</summary>
    [Range(1, 60 * 24 * 30)]
    public int ExpireMinutes { get; set; } = 480;
}
