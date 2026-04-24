using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MemoriaServer.Data;

/// <summary>
/// Design-time factory used by EF Core tooling (dotnet ef migrations add / update) so it
/// can instantiate the DbContext without spinning up the full ASP.NET Core host.
/// This avoids the fail-fast checks for Discord secrets and the DefaultConnection
/// connection string that would otherwise block migration generation.
/// </summary>
internal sealed class MemoriaDbContextFactory : IDesignTimeDbContextFactory<MemoriaDbContext>
{
    public MemoriaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MemoriaDbContext>();

        // Use a dummy Npgsql connection string so the migration is generated against
        // the correct Postgres provider. No actual connection is made at design time.
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=memoria_designtime;Username=design;Password=design",
            npgsql => npgsql.MigrationsAssembly("MemoriaServer"));

        return new MemoriaDbContext(optionsBuilder.Options);
    }
}
