using Microsoft.EntityFrameworkCore;
using AlphaScopeServer;
using AlphaScopeServer.Data;
using AlphaScopeServer.Middleware;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        options.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
    });

// Configure Entity Framework. In the Testing environment the test harness registers
// its own (InMemory) DbContext so we skip the Npgsql registration here — adding both
// providers to the same service provider makes EF Core throw "multiple providers".
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AlphaScopeDbContext>(options =>
    {
        var raw = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. " +
                "Set it via user-secrets, environment variable, or appsettings.json.");

        // Neon's dashboard hands out postgresql:// URIs by default; Npgsql expects
        // key-value. Convert transparently so operators can paste either form.
        var connectionString = ConnectionStringHelper.NormalizeForNpgsql(raw);

        options.UseNpgsql(connectionString, npgsql =>
        {
            // 30s gives Neon's serverless compute room to warm up on the first request after
            // an idle period. The previous 10s would 500-error the first plugin login of the day.
            npgsql.CommandTimeout(30);
            npgsql.MigrationsAssembly("AlphaScopeServer");
        });

        options.EnableSensitiveDataLogging(false);
        options.EnableServiceProviderCaching();
        options.EnableDetailedErrors(false);
    }, ServiceLifetime.Scoped);
}

// Configure CORS. No wildcard origins — the Dalamud plugin calls via RestSharp (not a browser),
// so CORS does not apply to it. Browser-based clients must be explicitly allowlisted via
// the `Cors:AllowedOrigins` config key (comma-separated). Unset = no browser origins allowed.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AlphaScopePolicy", policy =>
    {
        var originsConfig = builder.Configuration["Cors:AllowedOrigins"];
        var origins = string.IsNullOrWhiteSpace(originsConfig)
            ? Array.Empty<string>()
            : originsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (origins.Length == 0)
        {
            policy.WithOrigins("https://alphascope.invalid")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(origins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Add API Explorer and Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AlphaScope Server API", Version = "v1" });
});

// Configure logging with performance filters
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Information);

var app = builder.Build();

// Apply EF Core migrations at startup. Fail fast if they don't apply — starting
// the server with an out-of-sync schema is worse than refusing to start. Skipped
// in the Testing environment because the test harness swaps the DbContext to
// InMemory (which doesn't support Migrate) AFTER this code has already run.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    if (context.Database.IsRelational())
    {
        try
        {
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration failed — server cannot start");
            throw;
        }
    }
    else
    {
        logger.LogInformation("DbContext is non-relational (provider: {Provider}); skipping migrations", context.Database.ProviderName);
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AlphaScope Server API v1");
        c.RoutePrefix = "swagger";
    });
}

// Add CORS before authentication
app.UseCors("AlphaScopePolicy");

// API key authentication — required for all endpoints except the paths the middleware
// explicitly skips (GET /server, /users/login, /users/create-test-user, /auth/*,
// /waitforlogin, /swagger/*, /health).
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

// Add routing and controllers
app.UseRouting();
app.MapControllers();

// Add a simple health check endpoint
app.MapGet("/health", () => new { status = "healthy", timestamp = DateTimeOffset.UtcNow })
    .WithName("HealthCheck")
    .WithOpenApi();


app.Run();

// Program class for testing
public partial class Program { }