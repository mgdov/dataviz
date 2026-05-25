using System.Text;
using DataViz.Api.Auth;
using DataViz.Api.Data;
using DataViz.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration -----------------------------------------------------------

builder.Configuration.AddEnvironmentVariables();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? "Host=localhost;Port=5432;Database=dataviz;Username=dataviz;Password=dataviz";

builder.Services.AddDbContext<ApplicationDbContext>(opt => opt.UseNpgsql(connectionString));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// --- AuthN / AuthZ -----------------------------------------------------------

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Key))
    jwtOptions.Key = "DEV_ONLY_INSECURE_KEY_PLEASE_OVERRIDE_VIA_ENV_32_BYTES_MIN";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// --- CORS for the Next.js frontend ------------------------------------------

const string CorsPolicy = "WebClient";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
{
    var origins = builder.Configuration["Cors:Origins"]
                  ?? Environment.GetEnvironmentVariable("CORS_ORIGINS")
                  ?? "http://localhost:3000";
    p.WithOrigins(origins.Split(',', StringSplitOptions.RemoveEmptyEntries))
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials();
}));

// --- MVC / Swagger -----------------------------------------------------------

builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DataViz API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

// --- Pipeline ----------------------------------------------------------------

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var maxRetries = 10;
    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            logger.LogWarning(ex, "Migration attempt {Attempt} failed, retrying...", attempt);
            await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
        }
    }

    // Ensure a default admin user exists when the database has no admin.
    if (!await db.Users.AnyAsync(u => u.Role == "admin"))
    {
        var adminEmail = builder.Configuration["Admin:Email"]
                         ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL")
                         ?? "admin@example.com";
        var adminPassword = builder.Configuration["Admin:Password"]
                            ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD")
                            ?? "admin12345";
        var adminName = builder.Configuration["Admin:Name"]
                        ?? Environment.GetEnvironmentVariable("ADMIN_NAME")
                        ?? "Admin";

        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
        if (existingUser is null)
        {
            var adminUser = new DataViz.Api.Models.User
            {
                Name = adminName,
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = "admin",
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(adminUser);
            await db.SaveChangesAsync();
            logger.LogInformation("Created default admin user {Email}", adminEmail);
            logger.LogInformation("Admin password: {Password}", adminPassword);
        }
        else
        {
            existingUser.Role = "admin";
            existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
            await db.SaveChangesAsync();
            logger.LogInformation("Upgraded existing user {Email} to admin", adminEmail);
            logger.LogInformation("Admin password: {Password}", adminPassword);
        }
    }

    // Only seed data when explicitly enabled via environment variable or configuration.
    var seedEnabled = builder.Configuration.GetValue<bool>("SeedData", false)
                      || Environment.GetEnvironmentVariable("SEED_DATA") == "true";
    if (seedEnabled)
    {
        await DataSeeder.SeedAsync(db, logger);
    }
    else
    {
        logger.LogInformation("Data seeding skipped (SEED_DATA not set or SeedData=false)");
    }
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DataViz API v1"));

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
