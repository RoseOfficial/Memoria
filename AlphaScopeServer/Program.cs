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

// Configure Entity Framework with performance optimizations
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
        npgsql.CommandTimeout(10);
        npgsql.MigrationsAssembly("AlphaScopeServer");
    });

    options.EnableSensitiveDataLogging(false);
    options.EnableServiceProviderCaching();
    options.EnableDetailedErrors(false);
}, ServiceLifetime.Scoped);

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AlphaScopePolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add API Explorer and Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AlphaScope Server API", Version = "v1" });
    // API Key authentication removed - now public API
});

// Configure logging with performance filters
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Information);

var app = builder.Build();

// Apply EF Core migrations at startup. Fail fast if they don't apply — starting
// the server with an out-of-sync schema is worse than refusing to start.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
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

// Authentication removed - public API

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