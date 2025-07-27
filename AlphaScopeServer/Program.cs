using Microsoft.EntityFrameworkCore;
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
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=AlphaScope.db;Cache=Shared;Pooling=true";
    options.UseSqlite(connectionString, sqliteOptions =>
    {
        sqliteOptions.CommandTimeout(10); // Reduced timeout for faster failure detection
    });
    
    // Performance optimizations
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
builder.Logging.AddDebug();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Information);

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();
    try
    {
        context.Database.EnsureCreated();
        Console.WriteLine("Database initialized successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error initializing database: {ex.Message}");
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

// Log server information
app.Logger.LogInformation("AlphaScope Server starting...");
app.Logger.LogInformation("Server URL: https://localhost:5001");
app.Logger.LogInformation("Swagger UI: https://localhost:5001/swagger");

app.Run();

// Program class for testing
public partial class Program { }