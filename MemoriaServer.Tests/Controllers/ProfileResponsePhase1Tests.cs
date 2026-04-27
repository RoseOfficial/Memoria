using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MemoriaServer.Controllers;
using MemoriaServer.Data;
using MemoriaServer.Models.DTOs;
using MemoriaServer.Models.Entities;
using TestUtilities;

namespace MemoriaServer.Tests.Controllers;

public class ProfileResponsePhase1Tests
{
    private static (PlayersController controller, MemoriaDbContext ctx) Build()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>();
        var ctx = new MemoriaDbContext(options);
        ctx.Database.EnsureCreated();

        var logger = LoggerTestUtilities.CreateMockLogger<PlayersController>();
        var controller = new PlayersController(ctx, logger);

        var httpContext = new DefaultHttpContext();
        // Tier 1 anonymous viewer — no User needed for the public-read endpoint.
        httpContext.Items["Tier"] = 1;
        httpContext.Items["IsAdmin"] = false;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, ctx);
    }

    [Fact]
    public async Task Profile_includes_phase_1_scalars()
    {
        var (controller, ctx) = Build();
        ctx.Players.Add(new Player {
            LocalContentId = 200, Name = "Phase",
            HomeWorldId = 54,  // Faerie
            OnlineStatusId = 27, TitleId = 1234,
            GrandCompanyId = 2, FreeCompanyTag = "Memo",
            LodestonePortraitUrl = "https://img2.finalfantasyxiv.com/portrait.jpg",
        });
        await ctx.SaveChangesAsync();

        var result = await controller.GetBySlug("faerie", "Phase") as OkObjectResult;
        result.Should().NotBeNull();
        var dto = result!.Value as PlayerProfileResponse;
        dto.Should().NotBeNull();
        dto!.Header.OnlineStatusId.Should().Be(27);
        dto.Header.TitleId.Should().Be(1234);
        dto.Header.GrandCompanyId.Should().Be(2);
        dto.Header.FreeCompanyTag.Should().Be("Memo");
        dto.Header.PortraitUrl.Should().Be("https://img2.finalfantasyxiv.com/portrait.jpg");
    }

    [Fact]
    public async Task Profile_resolves_mount_name_and_icon_from_lodestone_json()
    {
        var (controller, ctx) = Build();
        ctx.Players.Add(new Player {
            LocalContentId = 201, Name = "Phase",
            HomeWorldId = 54,  // Faerie
            CurrentMountId = 45,
            LodestoneMountsData = "[{\"Name\":\"Logos Reaper\",\"IconUrl\":\"https://img.example/45.png\",\"MountId\":45,\"AcquiredDate\":null}]",
        });
        await ctx.SaveChangesAsync();

        var result = await controller.GetBySlug("faerie", "Phase") as OkObjectResult;
        var dto = result!.Value as PlayerProfileResponse;
        dto!.Header.CurrentMountName.Should().Be("Logos Reaper");
        dto.Header.CurrentMountIconUrl.Should().Be("https://img.example/45.png");
    }

    [Fact]
    public async Task Profile_falls_back_to_null_name_for_unowned_mount()
    {
        var (controller, ctx) = Build();
        ctx.Players.Add(new Player {
            LocalContentId = 202, Name = "Phase",
            HomeWorldId = 54,  // Faerie
            CurrentMountId = 999,  // not in the player's owned list
            LodestoneMountsData = "[{\"Name\":\"Logos Reaper\",\"IconUrl\":\"https://img.example/45.png\",\"MountId\":45,\"AcquiredDate\":null}]",
        });
        await ctx.SaveChangesAsync();

        var result = await controller.GetBySlug("faerie", "Phase") as OkObjectResult;
        var dto = result!.Value as PlayerProfileResponse;
        dto!.Header.CurrentMountName.Should().BeNull();
        dto.Header.CurrentMountIconUrl.Should().BeNull();
    }

    [Fact]
    public async Task Profile_minion_name_resolves_from_lodestone_json()
    {
        var (controller, ctx) = Build();
        ctx.Players.Add(new Player {
            LocalContentId = 203, Name = "Phase",
            HomeWorldId = 54,  // Faerie
            CurrentMinionId = 200,
            LodestoneMinionsData = "[{\"Name\":\"Wind-up Cid\",\"IconUrl\":\"https://img.example/200.png\",\"MinionId\":200,\"AcquiredDate\":null}]",
        });
        await ctx.SaveChangesAsync();

        var result = await controller.GetBySlug("faerie", "Phase") as OkObjectResult;
        var dto = result!.Value as PlayerProfileResponse;
        dto!.Header.CurrentMinionName.Should().Be("Wind-up Cid");
        dto.Header.CurrentMinionIconUrl.Should().Be("https://img.example/200.png");
    }

    [Fact]
    public async Task Profile_phase_1_scalars_default_to_null_when_unset()
    {
        var (controller, ctx) = Build();
        ctx.Players.Add(new Player {
            LocalContentId = 204, Name = "NoPhase1Data",
            HomeWorldId = 54,  // Faerie
        });
        await ctx.SaveChangesAsync();

        var result = await controller.GetBySlug("faerie", "NoPhase1Data") as OkObjectResult;
        var dto = result!.Value as PlayerProfileResponse;
        dto!.Header.OnlineStatusId.Should().BeNull();
        dto.Header.TitleId.Should().BeNull();
        dto.Header.GrandCompanyId.Should().BeNull();
        dto.Header.FreeCompanyTag.Should().BeNull();
        dto.Header.CurrentMountName.Should().BeNull();
        dto.Header.CurrentMinionName.Should().BeNull();
        dto.Header.PortraitUrl.Should().BeNull();
    }
}
