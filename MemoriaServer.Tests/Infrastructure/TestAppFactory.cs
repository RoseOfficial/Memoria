using System;
using System.Collections.Generic;
using System.Linq;
using MemoriaServer.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MemoriaServer.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory variant used by the integration tests. Three things:
/// 1. Sets the environment to "Testing" so Program.cs skips its one-shot migration step
///    (InMemory doesn't support Migrate and Program.cs's startup runs before the test
///    factory's service overrides get a chance to swap the DbContext).
/// 2. Feeds a placeholder connection string so Program.cs's null-check does not throw
///    during config read — the DbContext is replaced below so the string never connects.
/// 3. Replaces the Npgsql DbContext registration with a per-test InMemory one via
///    ConfigureTestServices, which runs AFTER Program.cs's AddDbContext call.
/// </summary>
public class TestAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=placeholder;Database=placeholder;Username=placeholder;Password=placeholder"
            });
        });

        // Program.cs skips its DbContext registration when the environment is "Testing",
        // so we register the InMemory one here with no provider conflict.
        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<MemoriaDbContext>(options =>
                options.UseInMemoryDatabase("Tests-" + Guid.NewGuid().ToString("N")));
        });
    }
}
