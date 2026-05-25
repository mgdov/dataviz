using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DataViz.Api.Infrastructure;

/// <summary>
/// Глобальный обработчик необработанных исключений.
/// Возвращает ответ в формате RFC 7807 (ProblemDetails) и логирует ошибку
/// с уникальным trace-id, чтобы её можно было найти в логах.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        _logger.LogError(exception,
            "Unhandled exception. TraceId={TraceId} Path={Path} Method={Method}",
            traceId, httpContext.Request.Path, httpContext.Request.Method);

        var problem = new ProblemDetails
        {
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = _env.IsDevelopment() ? exception.Message : "An unexpected error occurred.",
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
