using Microsoft.EntityFrameworkCore;

namespace AlphaScope.Database;

/// <summary>
/// Entity Framework database context for AlphaScope plugin's local SQLite database.
/// Manages Player and Retainer entities with their relationships and provides data access.
/// Used for local caching and offline functionality before data is synced to the server.
/// </summary>
internal sealed class RetainerTrackContext : DbContext
{
    /// <summary>
    /// Database set for retainer entities. Retainers are linked to players through OwnerLocalContentId.
    /// Stores retainer names, world IDs, and ownership information.
    /// </summary>
    public DbSet<Retainer> Retainers { get; set; }
    
    /// <summary>
    /// Database set for player entities. Players are tracked with their character customization data,
    /// current job information, and account linking data.
    /// </summary>
    public DbSet<Player> Players { get; set; }
    /// <summary>
    /// Initializes the database context with the provided options.
    /// Options typically include the SQLite connection string and other EF configuration.
    /// </summary>
    /// <param name="options">Entity Framework configuration options for this context</param>
    public RetainerTrackContext(DbContextOptions<RetainerTrackContext> options)
        : base(options)
    {
    }
}
