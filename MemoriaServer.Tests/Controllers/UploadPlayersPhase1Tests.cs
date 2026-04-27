using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MemoriaServer.Controllers;
using MemoriaServer.Data;
using MemoriaServer.Models.DTOs;
using MemoriaServer.Models.Entities;
using TestUtilities;

namespace MemoriaServer.Tests.Controllers;

public class UploadPlayersPhase1Tests
{
    private static (PlayersController controller, MemoriaDbContext ctx, ApplicationUser user) BuildController()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>();
        var ctx = new MemoriaDbContext(options);
        ctx.Database.EnsureCreated();

        var user = new ApplicationUser
        {
            Name = "Phase1Uploader",
            ApiKey = "PHASE1-TEST-KEY",
            PrimaryCharacterLocalContentId = 0,
            TotalContributions = 0,
        };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var logger = LoggerTestUtilities.CreateMockLogger<PlayersController>();
        var controller = new PlayersController(ctx, logger);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["User"] = user;
        httpContext.Items["UserId"] = user.Id;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, ctx, user);
    }

    [Fact]
    public async Task Insert_writes_all_phase_1_fields()
    {
        var (controller, ctx, _) = BuildController();

        await controller.UploadPlayers(new List<PostPlayerRequest>
        {
            new() {
                LocalContentId = 100, Name = "Phase Tester",
                OnlineStatusId = 27, TitleId = 1234, GrandCompanyId = 2,
                FreeCompanyTag = "Memo", CurrentMountId = 45, CurrentMinionId = 200,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            }
        });

        var stored = await ctx.Players.AsNoTracking().FirstAsync(p => p.LocalContentId == 100);
        stored.OnlineStatusId.Should().Be(27);
        stored.TitleId.Should().Be(1234);
        stored.GrandCompanyId.Should().Be(2);
        stored.FreeCompanyTag.Should().Be("Memo");
        stored.CurrentMountId.Should().Be(45);
        stored.CurrentMinionId.Should().Be(200);
    }

    [Fact]
    public async Task Update_overwrites_phase_1_fields_with_latest_values()
    {
        var (controller, ctx, _) = BuildController();
        ctx.Players.Add(new Player {
            LocalContentId = 101, Name = "X",
            OnlineStatusId = 1, CurrentMountId = 10, CurrentMinionId = 50,
            FreeCompanyTag = "OldT", GrandCompanyId = 1, TitleId = 100,
        });
        await ctx.SaveChangesAsync();

        await controller.UploadPlayers(new List<PostPlayerRequest>
        {
            new() {
                LocalContentId = 101, Name = "X",
                OnlineStatusId = 27, TitleId = 200, GrandCompanyId = 2,
                FreeCompanyTag = "NewT", CurrentMountId = 99, CurrentMinionId = 99,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            }
        });

        var stored = await ctx.Players.AsNoTracking().FirstAsync(p => p.LocalContentId == 101);
        stored.OnlineStatusId.Should().Be(27);
        stored.TitleId.Should().Be(200);
        stored.GrandCompanyId.Should().Be(2);
        stored.FreeCompanyTag.Should().Be("NewT");
        stored.CurrentMountId.Should().Be(99);
        stored.CurrentMinionId.Should().Be(99);
    }

    [Fact]
    public async Task Update_preserves_existing_value_when_request_sends_null()
    {
        // All Phase 1 fields use "latest non-null wins". A null in the request
        // means "not observed this scan" — never "explicitly clear".
        var (controller, ctx, _) = BuildController();
        ctx.Players.Add(new Player {
            LocalContentId = 102, Name = "X",
            CurrentMountId = 45, CurrentMinionId = 200,
            FreeCompanyTag = "OLD", GrandCompanyId = 1,
            OnlineStatusId = 5, TitleId = 50,
        });
        await ctx.SaveChangesAsync();

        await controller.UploadPlayers(new List<PostPlayerRequest>
        {
            new() {
                LocalContentId = 102, Name = "X",
                CurrentMountId = null, CurrentMinionId = null,
                FreeCompanyTag = null, GrandCompanyId = null,
                OnlineStatusId = null, TitleId = null,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            }
        });

        var stored = await ctx.Players.AsNoTracking().FirstAsync(p => p.LocalContentId == 102);
        stored.CurrentMountId.Should().Be(45);
        stored.CurrentMinionId.Should().Be(200);
        stored.FreeCompanyTag.Should().Be("OLD");
        stored.GrandCompanyId.Should().Be(1);
        stored.OnlineStatusId.Should().Be(5);
        stored.TitleId.Should().Be(50);
    }

    [Fact]
    public async Task Update_partial_payload_only_updates_observed_fields()
    {
        // Different capture paths see different field subsets. SocialList sees
        // GC + FC tag but not Title/Mount/Minion. The non-observed fields must
        // not be cleared by the partial update.
        var (controller, ctx, _) = BuildController();
        ctx.Players.Add(new Player {
            LocalContentId = 103, Name = "X",
            OnlineStatusId = 1, TitleId = 100, GrandCompanyId = 1,
            FreeCompanyTag = "TAGZ", CurrentMountId = 45, CurrentMinionId = 200,
        });
        await ctx.SaveChangesAsync();

        // Simulate a SocialList capture: it observed GC + FC tag, didn't observe
        // OnlineStatus/Title/Mount/Minion.
        await controller.UploadPlayers(new List<PostPlayerRequest>
        {
            new() {
                LocalContentId = 103, Name = "X",
                GrandCompanyId = 3, FreeCompanyTag = "NEW",
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            }
        });

        var stored = await ctx.Players.AsNoTracking().FirstAsync(p => p.LocalContentId == 103);
        stored.GrandCompanyId.Should().Be(3);
        stored.FreeCompanyTag.Should().Be("NEW");
        stored.OnlineStatusId.Should().Be(1);
        stored.TitleId.Should().Be(100);
        stored.CurrentMountId.Should().Be(45);
        stored.CurrentMinionId.Should().Be(200);
    }
}
