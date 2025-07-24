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

// Configure Entity Framework
builder.Services.AddDbContext<AlphaScopeDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=AlphaScope.db";
    options.UseSqlite(connectionString, sqliteOptions =>
    {
        sqliteOptions.CommandTimeout(30);
    });
});

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
    c.AddSecurityDefinition("ApiKey", new()
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Please enter API key in format: {key}-{accountId}",
        Name = "api-key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            new string[] {}
        }
    });
});

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

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

// Add custom authentication middleware
app.UseApiKeyAuthentication();

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