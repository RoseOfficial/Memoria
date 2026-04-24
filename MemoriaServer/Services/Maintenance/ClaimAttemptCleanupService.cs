using Microsoft.EntityFrameworkCore;
using MemoriaServer.Data;

namespace MemoriaServer.Services.Maintenance
{
    /// <summary>
    /// Hourly sweeper that deletes ClaimAttempt rows more than 24h past their ExpiresAt.
    /// The grace window keeps a recently-expired attempt around long enough for the user's
    /// "code expired — start again" message instead of "start a claim first."
    /// </summary>
    public sealed class ClaimAttemptCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ClaimAttemptCleanupService> _logger;

        public ClaimAttemptCleanupService(IServiceProvider serviceProvider, ILogger<ClaimAttemptCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(_serviceProvider, _logger, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ClaimAttemptCleanupService run failed");
                }
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Shutdown requested while awaiting next tick.
                    return;
                }
            }
        }

        public static async Task RunOnceAsync(IServiceProvider services, ILogger logger, CancellationToken ct)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();

            var cutoff = DateTime.UtcNow.AddHours(-24);
            var stale = await db.ClaimAttempts.Where(a => a.ExpiresAt < cutoff).ToListAsync(ct);
            if (stale.Count == 0) return;

            db.ClaimAttempts.RemoveRange(stale);
            var removed = await db.SaveChangesAsync(ct);
            logger.LogInformation("ClaimAttemptCleanupService removed {Count} expired rows", removed);
        }
    }
}
