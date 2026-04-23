using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlphaScopeServer.Tests.Infrastructure;

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
            services.AddSingleton(new AlphaScopeServer.Services.Auth.OAuthStateSigner(
                "ZGV2ZWxvcG1lbnQta2V5LTMyLWJ5dGVzLWxvbmctYmFzZTY0"));
        });
    }
}
