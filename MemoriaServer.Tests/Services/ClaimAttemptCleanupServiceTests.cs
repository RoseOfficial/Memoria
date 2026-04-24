using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MemoriaServer.Data;
using MemoriaServer.Models.Entities;
using MemoriaServer.Services.Maintenance;
using TestUtilities;

namespace MemoriaServer.Tests.Services;

public class ClaimAttemptCleanupServiceTests
{
    [Fact]
    public async Task RunOnce_DeletesRowsOlderThan24hGrace()
    {
        var dbName = "cleanup-" + Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<MemoriaDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();
            var u = new ApplicationUser { Name = "U", ApiKey = "k", PrimaryCharacterLocalContentId = 0 };
            var p = new Player { LocalContentId = 1, Name = "P" };
            db.Users.Add(u); db.Players.Add(p);
            db.SaveChanges();

            // Recently-expired: keep (within 24h grace)
            db.ClaimAttempts.Add(new ClaimAttempt { UserId = u.Id, PlayerLocalContentId = 1, Code = "AS-RECENT", ExpiresAt = DateTime.UtcNow.AddHours(-1) });
            // Well-expired: delete
            db.ClaimAttempts.Add(new ClaimAttempt { UserId = u.Id + 1, PlayerLocalContentId = 1, Code = "AS-OLD", ExpiresAt = DateTime.UtcNow.AddHours(-48) });
            db.SaveChanges();
        }

        await ClaimAttemptCleanupService.RunOnceAsync(sp, NullLogger<ClaimAttemptCleanupService>.Instance, CancellationToken.None);

        using var scope2 = sp.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<MemoriaDbContext>();
        db2.ClaimAttempts.Select(a => a.Code).ToList().Should().BeEquivalentTo(new[] { "AS-RECENT" });
    }
}
