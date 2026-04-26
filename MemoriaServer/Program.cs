using Microsoft.EntityFrameworkCore;
using MemoriaServer;
using MemoriaServer.Data;
using MemoriaServer.Middleware;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
        options.SerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
        options.SerializerSettings.ContractResolver =
            new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
    });

// Configure Entity Framework. In the Testing environment the test harness registers
// its own (InMemory) DbContext so we skip the Npgsql registration here — adding both
// providers to the same service provider makes EF Core throw "multiple providers".
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<MemoriaDbContext>(options =>
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
            npgsql.MigrationsAssembly("MemoriaServer");
        });

        options.EnableSensitiveDataLogging(false);
        options.EnableServiceProviderCaching();
        options.EnableDetailedErrors(false);
    }, ServiceLifetime.Scoped);
}

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddSingleton<MemoriaServer.Services.Lodestone.ILodestoneBioFetcher,
                                   MemoriaServer.Services.Lodestone.NetStoneLodestoneBioFetcher>();
    builder.Services.AddHostedService<MemoriaServer.Services.Maintenance.ClaimAttemptCleanupService>();

    // Centralized Lodestone enrichment. Singleton + HostedService registration so the
    // PlayersController upload path can resolve the service to enqueue freshly-uploaded
    // players, while the same instance runs the background processing loop.
    builder.Services.AddSingleton<MemoriaServer.Services.Lodestone.LodestoneEnrichmentService>();
    builder.Services.AddHostedService(sp =>
        sp.GetRequiredService<MemoriaServer.Services.Lodestone.LodestoneEnrichmentService>());
}

builder.Services.AddSingleton<MemoriaServer.Services.Takedowns.TakedownRateLimiter>();

// Configure CORS. No wildcard origins — the Dalamud plugin calls via RestSharp (not a browser),
// so CORS does not apply to it. Browser-based clients must be explicitly allowlisted via
// the `Cors:AllowedOrigins` config key (array) or match `Cors:AllowedOriginPattern` (regex).
// Netlify preview URLs (https://<slug>--memoriagg.netlify.app) are covered by the pattern;
// production origin (https://memoria.gg) lives in AllowedOrigins. AllowCredentials is
// required for the __Host-memoria cookie — this is incompatible with AllowAnyOrigin(), so we
// use SetIsOriginAllowed with an explicit delegate instead.
builder.Services.AddCors(options =>
{
    options.AddPolicy("MemoriaPolicy", policy =>
    {
        var exactOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();
        var pattern = builder.Configuration.GetValue<string>("Cors:AllowedOriginPattern");
        var regex = string.IsNullOrWhiteSpace(pattern)
            ? null
            : new System.Text.RegularExpressions.Regex(
                pattern,
                System.Text.RegularExpressions.RegexOptions.Compiled);

        policy.SetIsOriginAllowed(origin =>
                exactOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) ||
                (regex != null && regex.IsMatch(origin)))
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Strongly-typed Discord OAuth options. Fail fast at boot if any field missing.
builder.Services.Configure<MemoriaServer.Services.Auth.DiscordOptions>(
    builder.Configuration.GetSection(MemoriaServer.Services.Auth.DiscordOptions.Section));

// Admin Discord user allowlist.
builder.Services.Configure<MemoriaServer.Services.Admin.AdminOptions>(
    builder.Configuration.GetSection(MemoriaServer.Services.Admin.AdminOptions.SectionName));

// Register OAuthStateSigner unconditionally so AuthController can be constructed even when
// Discord isn't configured. When Discord is off the controller short-circuits with 503 before
// touching the signer, so the throwaway random key never gets used.
if (!builder.Environment.IsEnvironment("Testing"))
{
    var discord = builder.Configuration.GetSection(MemoriaServer.Services.Auth.DiscordOptions.Section)
        .Get<MemoriaServer.Services.Auth.DiscordOptions>()
        ?? new MemoriaServer.Services.Auth.DiscordOptions();

    if (discord.IsConfigured)
    {
        var serverBaseUrl = builder.Configuration["ServerBaseUrl"];
        if (string.IsNullOrWhiteSpace(serverBaseUrl))
            throw new InvalidOperationException(
                "ServerBaseUrl is required when Discord OAuth is configured (used for the OAuth callback URL).");

        builder.Services.AddSingleton(new MemoriaServer.Services.Auth.OAuthStateSigner(discord.StateSigningKey));
    }
    else
    {
        Console.WriteLine(
            "[Memoria] Discord OAuth is not configured — auth/discord/* and auth/link/* endpoints will return 503. " +
            "Set Discord:ClientId, Discord:ClientSecret, Discord:GuildId, Discord:StateSigningKey, and ServerBaseUrl to enable login.");

        var throwaway = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        builder.Services.AddSingleton(new MemoriaServer.Services.Auth.OAuthStateSigner(throwaway));
    }
}

// Named HttpClient for Discord API calls. Tests override with a stubbed HttpMessageHandler.
builder.Services.AddHttpClient("DiscordAuth", c =>
{
    c.BaseAddress = new Uri("https://discord.com/api/");
    c.DefaultRequestHeaders.Add("User-Agent", "Memoria/1.0");
});

// Add API Explorer and Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Memoria Server API", Version = "v1" });
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
    var context = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Memoria Server API v1");
        c.RoutePrefix = "swagger";
    });
}

// Add CORS before authentication
app.UseCors("MemoriaPolicy");

// API key authentication — required for all endpoints except the paths the middleware
// explicitly skips (GET /server, /users/login, /users/create-test-user, /auth/*,
// /swagger/*, /health).
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseMiddleware<TierResolutionMiddleware>();

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