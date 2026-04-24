using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MemoriaServer.Tests.TestDoubles;

namespace MemoriaServer.Tests.Infrastructure;

/// <summary>
/// TestAppFactory variant that seeds Discord OAuth config so the OAuth endpoints exercise
/// the fail-fast path with real values. Returns-to allowlist is tied to app.example.com.
/// </summary>
public class AuthControllerOAuthFactory : TestAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discord:ClientId"] = "test-client-id",
                ["Discord:ClientSecret"] = "test-client-secret",
                ["Discord:GuildId"] = "999888777",
                ["Discord:StateSigningKey"] = "ZGV2ZWxvcG1lbnQta2V5LTMyLWJ5dGVzLWxvbmctYmFzZTY0",
                ["ServerBaseUrl"] = "https://api.example.com",
                ["Cors:AllowedOrigins"] = "https://app.example.com",
            });
        });

        // The base TestAppFactory sets UseEnvironment("Testing"), which causes Program.cs to
        // skip OAuthStateSigner registration. Register it manually here so AuthController
        // can resolve it during integration tests.
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(new MemoriaServer.Services.Auth.OAuthStateSigner(
                "ZGV2ZWxvcG1lbnQta2V5LTMyLWJ5dGVzLWxvbmctYmFzZTY0"));
        });
    }

    /// <summary>
    /// Returns a factory variant that replaces the "DiscordAuth" named HttpClient with
    /// one backed by the supplied stub handler. Returns the base WebApplicationFactory type
    /// because WithWebHostBuilder does not preserve the subclass type.
    /// </summary>
    public WebApplicationFactory<Program> WithDiscordHandler(StubDiscordHttpHandler handler)
    {
        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("DiscordAuth")
                        .ConfigurePrimaryHttpMessageHandler(() => handler);
            });
        });
    }
}
