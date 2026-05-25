using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using DataViz.Api.Auth;
using DataViz.Api.Data;
using DataViz.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

const string CorsPolicy = "WebClient";
const string AuthRateLimitPolicy = "auth-strict";

var builder = WebApplication.CreateBuilder(args);

// --- Configuration -----------------------------------------------------------
// Переменные окружения имеют приоритет над appsettings, что удобно для Docker и Kubernetes.
builder.Configuration.AddEnvironmentVariables();

// --- Database ----------------------------------------------------------------
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=dataviz;Username=dataviz;Password=dataviz";

builder.Services.AddDbContextPool<ApplicationDbContext>(opt =>
{
    opt.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
        npgsql.CommandTimeout(30);
    });

    if (builder.Environment.IsDevelopment())
        opt.EnableDetailedErrors().EnableSensitiveDataLogging();
});

// --- JWT options & token service ---------------------------------------------
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        opt => !string.Equals(opt.Key,
            "DEV_ONLY_INSECURE_KEY_PLEASE_OVERRIDE_VIA_ENV_32_BYTES_MIN",
            StringComparison.Ordinal),
        "Jwt:Key must be overridden — the bundled placeholder is not acceptable")
    .ValidateOnStart();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// --- Authentication / Authorization ------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Параметры подтягиваются из IOptionsMonitor<JwtOptions> чтобы избежать
        // дублирования логики "взять секцию Jwt и накормить её JwtBearer".
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var opt = ctx.HttpContext.RequestServices
                    .GetRequiredService<IOptionsMonitor<JwtOptions>>().CurrentValue;
                ctx.Options.TokenValidationParameters.ValidIssuer = opt.Issuer;
                ctx.Options.TokenValidationParameters.ValidAudience = opt.Audience;
                ctx.Options.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Key));
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.AdminOnly,
        policy => policy.RequireRole(UserRoles.Admin));

// --- CORS --------------------------------------------------------------------
var corsOrigins = (builder.Configuration["Cors:Origins"]
                   ?? Environment.GetEnvironmentVariable("CORS_ORIGINS")
                   ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .WithExposedHeaders("Location")));

// --- Rate limiting -----------------------------------------------------------
// Защита от bruteforce на /api/auth/* — 10 запросов в минуту с одного IP.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthRateLimitPolicy, ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

// --- MVC + JSON + ProblemDetails --------------------------------------------
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
    {
        // ASP.NET сам возвращает RFC 7807 для 400 при валидации модели — оставляем как есть.
        o.SuppressMapClientErrors = false;
    })
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opt.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddProblemDetails(o =>
{
    o.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// --- Health checks -----------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString,
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "db" });

// --- Swagger -----------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DataViz API",
        Version = "v1",
        Description = "REST API для системы визуализации данных (курсовая работа).",
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

// --- Build pipeline ----------------------------------------------------------
var app = builder.Build();

// Жёсткая валидация JWT-ключа в Production: лучше сразу упасть, чем работать с дев-ключом.
if (app.Environment.IsProduction())
{
    var jwt = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
    if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
        throw new InvalidOperationException(
            "Jwt:Key must be at least 32 bytes in Production. Set the JWT_KEY environment variable.");
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration);

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DataViz API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "DataViz API";
});

app.UseCors(CorsPolicy);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Health endpoints: /healthz — liveness (без БД), /readyz — readiness (с БД).
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthResponse,
}).AllowAnonymous();

app.MapHealthChecks("/readyz", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse,
}).AllowAnonymous();

app.MapControllers();

app.Run();

static Task WriteHealthResponse(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            durationMs = e.Value.Duration.TotalMilliseconds,
            description = e.Value.Description,
        }),
    };
    return ctx.Response.WriteAsJsonAsync(payload);
}

// Делаем тип Program доступным для интеграционных тестов через WebApplicationFactory<Program>.
public partial class Program;
