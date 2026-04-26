using MemoriaServer.Controllers;
using MemoriaServer.Data;
using MemoriaServer.Models.DTOs;
using MemoriaServer.Models.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities;
using Xunit;

namespace MemoriaServer.Tests.Controllers;

public class PlayersControllerBySlugTests
{
    private static PlayersController MakeController(MemoriaDbContext ctx, int? viewerUserId = null)
    {
        var c = new PlayersController(ctx, NullLogger<PlayersController>.Instance);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        c.HttpContext.Items["Tier"] = 1;
        c.HttpContext.Items["ViewerUserId"] = viewerUserId;
        return c;
    }

    [Fact]
    public async Task GetBySlug_CurrentMatch_Returns200WithHeader()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 1,
            Name = "Tataru Taru",
            HomeWorldId = 91,  // Balmung
            CurrentWorldId = 91,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "tataru-taru");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<PlayerProfileResponse>().Subject;
        dto.Header.Name.Should().Be("Tataru Taru");
        dto.Header.WorldSlug.Should().Be("balmung");
        dto.Header.WorldName.Should().Be("Balmung");
    }

    [Fact]
    public async Task GetBySlug_UnknownSlug_Returns404()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "nobody");
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBySlug_UnknownWorld_Returns404()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("notaworld", "tataru-taru");
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBySlug_Tier1_OmitsTier2Sections()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 1,
            Name = "Tataru Taru",
            HomeWorldId = 91,
            AccountId = 500,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "tataru-taru");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerProfileResponse>().Subject;

        dto.Locations.Should().BeNull();
        dto.NameHistory.Should().BeNull();
        dto.WorldHistory.Should().BeNull();
        dto.Alts.Should().BeNull();
    }

    [Fact]
    public async Task GetBySlug_ApostropheInName_MatchesViaSlug()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 1,
            Name = "T'chai Nunh",
            HomeWorldId = 91,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "tchai-nunh");
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBySlug_HistoricName_Returns301ToCurrent()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var player = new Player { LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91 };
        ctx.Players.Add(player);
        ctx.Set<PlayerNameHistory>().Add(new PlayerNameHistory
        {
            PlayerLocalContentId = 1, Name = "Tata Taru", CreatedAt = DateTime.UtcNow.AddDays(-30),
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "tata-taru");

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Permanent.Should().BeTrue();
        redirect.Url.Should().Be("/p/balmung/tataru-taru");
    }

    [Fact]
    public async Task GetBySlug_HistoricWorld_Returns301ToCurrent()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var player = new Player { LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91 };
        ctx.Players.Add(player);
        ctx.Set<PlayerWorldHistory>().Add(new PlayerWorldHistory
        {
            PlayerLocalContentId = 1, WorldId = 54 /* Faerie */, CreatedAt = DateTime.UtcNow.AddDays(-60),
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("faerie", "tataru-taru");
        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("/p/balmung/tataru-taru");
    }

    [Fact]
    public async Task GetBySlug_HideEntirely_Returns404ToAnon()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91, HideEntirely = true,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx, viewerUserId: null);
        var result = await controller.GetBySlug("balmung", "tataru-taru");
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBySlug_HideEntirely_Returns200ToOwner()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Users.Add(new ApplicationUser { Id = 7, DiscordUserId = 1, ApiKey = "x" });
        ctx.Players.Add(new Player
        {
            LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91,
            HideEntirely = true, ClaimedByUserId = 7,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx, viewerUserId: 7);
        var result = await controller.GetBySlug("balmung", "tataru-taru");
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetBySlug_WithCustomization_PopulatesCustomizationSection()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var player = new Player { LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91 };
        ctx.Players.Add(player);
        ctx.Set<PlayerCustomizationHistory>().Add(new PlayerCustomizationHistory
        {
            PlayerLocalContentId = 1,
            GenderRace = 5,
            Face = 2,
            SkinColor = 4,
            EyeShape = 1,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "tataru-taru");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerProfileResponse>().Subject;

        dto.Customization.Should().NotBeNull();
        dto.Customization!.Face.Should().Be(2);
        dto.Customization.SkinColor.Should().Be(4);
    }

    [Fact]
    public async Task GetBySlug_WithMountsJson_PopulatesMountsSection()
    {
        // The wire format produced by both the plugin's LodestoneRefreshService and the
        // server's LodestoneEnrichmentService is an array of objects with Name/IconUrl/etc.,
        // not a flat array of strings — the simple-string form was a speculative shape that
        // never landed in production data. Test against the format we actually serialize.
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91,
            LodestoneMountsData = """[{"MountId":1,"Name":"Company Chocobo","IconUrl":null,"AcquiredDate":null},{"MountId":45,"Name":"Black Chocobo","IconUrl":null,"AcquiredDate":null},{"MountId":102,"Name":"Magitek Armor","IconUrl":null,"AcquiredDate":null}]""",
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        var result = await controller.GetBySlug("balmung", "tataru-taru");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerProfileResponse>().Subject;

        dto.Mounts.Should().NotBeNull();
        dto.Mounts!.Collected.Should().Be(3);
        dto.Mounts.Preview.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetBySlug_Tier2_PopulatesLocationsAndHistory()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var player = new Player { LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91 };
        ctx.Players.Add(player);
        ctx.Set<PlayerNameHistory>().Add(new PlayerNameHistory
        {
            PlayerLocalContentId = 1, Name = "Tata Taru", CreatedAt = DateTime.UtcNow.AddDays(-30),
        });
        ctx.Set<PlayerTerritoryHistory>().Add(new PlayerTerritoryHistory
        {
            PlayerLocalContentId = 1, TerritoryId = 128, FirstSeenAt = DateTime.UtcNow.AddDays(-1), LastSeenAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx);
        controller.HttpContext.Items["Tier"] = 2;
        var result = await controller.GetBySlug("balmung", "tataru-taru");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerProfileResponse>().Subject;

        dto.Locations.Should().NotBeNull();
        dto.Locations!.Top.Should().HaveCount(1);
        dto.NameHistory.Should().NotBeNull();
        dto.NameHistory!.Should().HaveCount(1);
        dto.NameHistory[0].Name.Should().Be("Tata Taru");
    }

    [Fact]
    public async Task GetBySlug_Owner_SeesLocationsEvenWithHideEncountersTrue()
    {
        // Privacy toggles are about who-else-can-see, not "blind myself to my own
        // profile." Without this bypass the owner's /p/ view shows the same TierGate
        // a stranger would see, and the privacy toggles look like they broke things.
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.Add(new Player
        {
            LocalContentId = 1, Name = "Tataru Taru", HomeWorldId = 91,
            ClaimedByUserId = 7, HideEncounters = true,
        });
        ctx.Set<PlayerTerritoryHistory>().Add(new PlayerTerritoryHistory
        {
            PlayerLocalContentId = 1, TerritoryId = 128, FirstSeenAt = DateTime.UtcNow.AddDays(-1), LastSeenAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx, viewerUserId: 7);
        controller.HttpContext.Items["Tier"] = 2;
        var result = await controller.GetBySlug("balmung", "tataru-taru");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerProfileResponse>().Subject;

        dto.Locations.Should().NotBeNull("the owner must still see their own locations");
        dto.Locations!.Top.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlug_Owner_SeesAltsEvenWithHideAltsTrue()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.AddRange(
            new Player { LocalContentId = 1, Name = "Owner Char",  HomeWorldId = 91, ClaimedByUserId = 7, AccountId = 12345, HideAlts = true },
            new Player { LocalContentId = 2, Name = "Owner Alt",   HomeWorldId = 91, AccountId = 12345 }
        );
        await ctx.SaveChangesAsync();

        var controller = MakeController(ctx, viewerUserId: 7);
        controller.HttpContext.Items["Tier"] = 2;
        var result = await controller.GetBySlug("balmung", "owner-char");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerProfileResponse>().Subject;

        dto.Alts.Should().NotBeNull();
        dto.Alts!.Single().Name.Should().Be("Owner Alt");
    }

    [Fact]
    public async Task GetBySlug_Stranger_StillBlockedByHideAltsAndHideEncounters()
    {
        // Symmetry check — the owner-bypass must NOT leak through to other viewers.
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.Players.AddRange(
            new Player { LocalContentId = 1, Name = "Owner Char", HomeWorldId = 91, ClaimedByUserId = 7, AccountId = 12345, HideAlts = true, HideEncounters = true },
            new Player { LocalContentId = 2, Name = "Owner Alt",  HomeWorldId = 91, AccountId = 12345 }
        );
        ctx.Set<PlayerTerritoryHistory>().Add(new PlayerTerritoryHistory
        {
            PlayerLocalContentId = 1, TerritoryId = 128, FirstSeenAt = DateTime.UtcNow.AddDays(-1), LastSeenAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        // Different viewer (not the owner, not admin).
        var controller = MakeController(ctx, viewerUserId: 99);
        controller.HttpContext.Items["Tier"] = 2;
        var result = await controller.GetBySlug("balmung", "owner-char");
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<PlayerProfileResponse>().Subject;

        dto.Locations.Should().BeNull("strangers honor HideEncounters");
        dto.Alts.Should().BeNull("strangers honor HideAlts");
    }
}
