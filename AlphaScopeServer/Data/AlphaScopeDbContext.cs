using Microsoft.EntityFrameworkCore;
using AlphaScopeServer.Models.Entities;

namespace AlphaScopeServer.Data
{
    /// <summary>
    /// Entity Framework database context for the AlphaScope server application.
    /// Manages all player and user data with comprehensive relationship mappings,
    /// history tracking, and performance optimizations through strategic indexing.
    /// Supports both SQLite and SQL Server database providers.
    /// </summary>
    public class AlphaScopeDbContext : DbContext
    {
        /// <summary>
        /// Initializes the database context with the provided configuration options.
        /// </summary>
        /// <param name="options">Entity Framework configuration options</param>
        public AlphaScopeDbContext(DbContextOptions<AlphaScopeDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Additional configuration for the database context.
        /// Currently delegates to base implementation but available for future enhancements.
        /// </summary>
        /// <param name="optionsBuilder">Options builder for additional configuration</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        /// <summary>
        /// Enables foreign key constraints for SQLite databases.
        /// SQLite requires explicit enabling of foreign key enforcement.
        /// No action needed for SQL Server as it enforces foreign keys by default.
        /// </summary>
        private void EnableForeignKeys()
        {
            if (Database.IsSqlite())
            {
                Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
            }
        }

        // ========== PLAYER DATA ENTITIES ==========
        /// <summary>Primary player entity containing current character information</summary>
        public DbSet<Player> Players { get; set; }
        /// <summary>Historical record of player name changes</summary>
        public DbSet<PlayerNameHistory> PlayerNameHistory { get; set; }
        /// <summary>Historical record of player world transfers</summary>
        public DbSet<PlayerWorldHistory> PlayerWorldHistory { get; set; }
        /// <summary>Historical record of character appearance/customization changes</summary>
        public DbSet<PlayerCustomizationHistory> PlayerCustomizationHistory { get; set; }
        /// <summary>Historical record of territories/zones where players have been seen</summary>
        public DbSet<PlayerTerritoryHistory> PlayerTerritoryHistory { get; set; }
        /// <summary>Links between players and their Lodestone profiles</summary>
        public DbSet<PlayerLodestone> PlayerLodestones { get; set; }
        /// <summary>Record of when player profiles were viewed/accessed</summary>
        public DbSet<PlayerProfileVisit> PlayerProfileVisits { get; set; }
        
        
        // ========== USER AND AUTHENTICATION ENTITIES ==========
        /// <summary>Application users with authentication and profile data</summary>
        public DbSet<ApplicationUser> Users { get; set; }
        /// <summary>Links between users and their FFXIV characters</summary>
        public DbSet<UserCharacter> UserCharacters { get; set; }
        /// <summary>Links between users and their claimed Lodestone characters</summary>
        public DbSet<UserLodestoneCharacter> UserLodestoneCharacters { get; set; }

        /// <summary>
        /// Configures entity relationships, constraints, and database schema.
        /// Defines foreign keys, indexes, and cascading delete behaviors for all entities.
        /// Optimizes database performance through strategic index placement.
        /// </summary>
        /// <param name="modelBuilder">Entity Framework model builder for schema configuration</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Player configurations
            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(e => e.LocalContentId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.AccountId);
                entity.HasIndex(e => e.HomeWorldId);
                entity.HasIndex(e => e.CurrentWorldId);
                entity.HasIndex(e => e.CreatedAt);
            });

            // Player history relationships
            modelBuilder.Entity<PlayerNameHistory>(entity =>
            {
                entity.HasOne(e => e.Player)
                    .WithMany(p => p.NameHistory)
                    .HasForeignKey(e => e.PlayerLocalContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.PlayerLocalContentId);
                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<PlayerWorldHistory>(entity =>
            {
                entity.HasOne(e => e.Player)
                    .WithMany(p => p.WorldHistory)
                    .HasForeignKey(e => e.PlayerLocalContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.PlayerLocalContentId);
                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<PlayerCustomizationHistory>(entity =>
            {
                entity.HasOne(e => e.Player)
                    .WithMany(p => p.CustomizationHistory)
                    .HasForeignKey(e => e.PlayerLocalContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.PlayerLocalContentId);
                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<PlayerTerritoryHistory>(entity =>
            {
                entity.HasOne(e => e.Player)
                    .WithMany(p => p.TerritoryHistory)
                    .HasForeignKey(e => e.PlayerLocalContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.PlayerLocalContentId);
                entity.HasIndex(e => e.FirstSeenAt);
                entity.HasIndex(e => e.LastSeenAt);
            });

            modelBuilder.Entity<PlayerLodestone>(entity =>
            {
                entity.HasKey(e => e.PlayerLocalContentId);
                entity.HasOne(e => e.Player)
                    .WithOne(p => p.Lodestone)
                    .HasForeignKey<PlayerLodestone>(e => e.PlayerLocalContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.LodestoneId).IsUnique();
            });

            modelBuilder.Entity<PlayerProfileVisit>(entity =>
            {
                entity.HasOne(e => e.Player)
                    .WithMany(p => p.ProfileVisits)
                    .HasForeignKey(e => e.PlayerLocalContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.PlayerLocalContentId);
                entity.HasIndex(e => e.VisitedAt);
            });

            // User configurations
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ApiKey).IsRequired().HasMaxLength(256);
                entity.HasIndex(e => e.GameAccountId).IsUnique();
                entity.HasIndex(e => e.ApiKey).IsUnique();
                entity.HasIndex(e => e.PrimaryCharacterLocalContentId);
            });

            modelBuilder.Entity<UserCharacter>(entity =>
            {
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Characters)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.LocalContentId);
                entity.HasIndex(e => e.UserId);
            });

            modelBuilder.Entity<UserLodestoneCharacter>(entity =>
            {
                entity.HasOne(e => e.User)
                    .WithMany(u => u.LodestoneCharacters)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.LodestoneId).IsUnique();
                entity.HasIndex(e => e.UserId);
            });
        }

        /// <summary>
        /// Saves changes to the database with automatic timestamp updates and foreign key enforcement.
        /// Overrides base implementation to ensure data integrity and audit trail maintenance.
        /// </summary>
        /// <returns>Number of entities written to the database</returns>
        public override int SaveChanges()
        {
            EnableForeignKeys();
            UpdateTimestamps();
            return base.SaveChanges();
        }

        /// <summary>
        /// Asynchronously saves changes to the database with automatic timestamp updates and foreign key enforcement.
        /// Overrides base implementation to ensure data integrity and audit trail maintenance.
        /// </summary>
        /// <param name="cancellationToken">Token for cancelling the async operation</param>
        /// <returns>Task returning the number of entities written to the database</returns>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            EnableForeignKeys();
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Automatically updates CreatedAt and UpdatedAt timestamps for Player entities.
        /// Called before saving changes to maintain accurate audit trails.
        /// Sets CreatedAt for new entities and UpdatedAt for all modified entities.
        /// </summary>
        private void UpdateTimestamps()
        {
            var entities = ChangeTracker.Entries()
                .Where(x => x.Entity is Player && (x.State == EntityState.Added || x.State == EntityState.Modified));

            foreach (var entity in entities)
            {
                if (entity.State == EntityState.Added)
                {
                    ((Player)entity.Entity).CreatedAt = DateTime.UtcNow;
                }
                ((Player)entity.Entity).UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}