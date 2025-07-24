using Microsoft.EntityFrameworkCore;
using AlphaScopeServer.Models.Entities;

namespace AlphaScopeServer.Data
{
    public class AlphaScopeDbContext : DbContext
    {
        public AlphaScopeDbContext(DbContextOptions<AlphaScopeDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        private void EnableForeignKeys()
        {
            if (Database.IsSqlite())
            {
                Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
            }
        }

        // DbSets
        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerNameHistory> PlayerNameHistory { get; set; }
        public DbSet<PlayerWorldHistory> PlayerWorldHistory { get; set; }
        public DbSet<PlayerCustomizationHistory> PlayerCustomizationHistory { get; set; }
        public DbSet<PlayerTerritoryHistory> PlayerTerritoryHistory { get; set; }
        public DbSet<PlayerLodestone> PlayerLodestones { get; set; }
        public DbSet<PlayerProfileVisit> PlayerProfileVisits { get; set; }
        
        public DbSet<Retainer> Retainers { get; set; }
        public DbSet<RetainerNameHistory> RetainerNameHistory { get; set; }
        public DbSet<RetainerWorldHistory> RetainerWorldHistory { get; set; }
        
        public DbSet<ApplicationUser> Users { get; set; }
        public DbSet<UserCharacter> UserCharacters { get; set; }
        public DbSet<UserLodestoneCharacter> UserLodestoneCharacters { get; set; }

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

            // Retainer configurations
            modelBuilder.Entity<Retainer>(entity =>
            {
                entity.HasKey(e => e.LocalContentId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(24);
                entity.HasOne(e => e.Owner)
                    .WithMany(p => p.Retainers)
                    .HasForeignKey(e => e.OwnerLocalContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.WorldId);
                entity.HasIndex(e => e.OwnerLocalContentId);
                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<RetainerNameHistory>(entity =>
            {
                entity.HasOne(e => e.Retainer)
                    .WithMany(r => r.NameHistory)
                    .HasForeignKey(e => e.RetainerLocalContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.RetainerLocalContentId);
                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<RetainerWorldHistory>(entity =>
            {
                entity.HasOne(e => e.Retainer)
                    .WithMany(r => r.WorldHistory)
                    .HasForeignKey(e => e.RetainerLocalContentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.RetainerLocalContentId);
                entity.HasIndex(e => e.CreatedAt);
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

        public override int SaveChanges()
        {
            EnableForeignKeys();
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            EnableForeignKeys();
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

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