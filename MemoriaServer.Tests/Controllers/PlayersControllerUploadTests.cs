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

public class PlayersControllerUploadTests : IDisposable
{
    private readonly MemoriaDbContext _context;
    private readonly PlayersController _controller;
    private readonly ApplicationUser _user;

    public PlayersControllerUploadTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>();
        _context = new MemoriaDbContext(options);
        _context.Database.EnsureCreated();

        _user = new ApplicationUser
        {
            Name = "Uploader",
            ApiKey = "TEST-UPLOAD-KEY",
            PrimaryCharacterLocalContentId = 0,
            TotalContributions = 0,
        };
        _context.Users.Add(_user);
        _context.SaveChanges();

        var logger = LoggerTestUtilities.CreateMockLogger<PlayersController>();
        _controller = new PlayersController(_context, logger);

        var httpContext = new DefaultHttpContext();
        httpContext.Items["User"] = _user;
        httpContext.Items["UserId"] = _user.Id;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task UploadPlayers_IncrementsAuthenticatedUsersLifetimeContributions()
    {
        // Bug pre-fix: ApplicationUser.TotalContributions was declared and read by
        // GET /v1/users/me/contributions but never written, so the web dashboard
        // showed 0 for everyone regardless of how many scans they had uploaded.
        var ts = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var batch = new List<PostPlayerRequest>
        {
            new() { LocalContentId = 11, Name = "PlayerA", HomeWorldId = 34, CreatedAt = ts },
            new() { LocalContentId = 22, Name = "PlayerB", HomeWorldId = 65, CreatedAt = ts },
            new() { LocalContentId = 33, Name = "PlayerC", HomeWorldId = 91, CreatedAt = ts },
        };

        await _controller.UploadPlayers(batch);

        var fresh = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == _user.Id);
        fresh.TotalContributions.Should().Be(3);
    }

    [Fact]
    public async Task UploadPlayers_UpsertsTerritoryName_FromBatch()
    {
        // The plugin resolves TerritoryName via Lumina at scan time and ships it
        // alongside TerritoryId. Server upserts the lookup so the Locations panel
        // can render "Limsa Lominsa Upper Decks" instead of "Territory 129".
        var ts = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var batch = new List<PostPlayerRequest>
        {
            new() { LocalContentId = 11, Name = "A", HomeWorldId = 34, TerritoryId = 129, TerritoryName = "Limsa Lominsa Upper Decks", CreatedAt = ts },
            new() { LocalContentId = 22, Name = "B", HomeWorldId = 34, TerritoryId = 129, TerritoryName = "Limsa Lominsa Upper Decks", CreatedAt = ts },
        };

        await _controller.UploadPlayers(batch);

        var rows = await _context.TerritoryNames.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1, "duplicate (id, name) pairs in one batch should dedupe to one row");
        rows[0].TerritoryId.Should().Be(129);
        rows[0].Name.Should().Be("Limsa Lominsa Upper Decks");
    }

    [Fact]
    public async Task UploadPlayers_TerritoryNameRename_UpdatesExistingRow()
    {
        // If Square Enix renames a zone (rare but real — happened with Sea of Clouds
        // pre-Heavensward in some translations), later scans should overwrite the
        // stale name. Existing TerritoryNames rows therefore have to be mutable, not
        // insert-only.
        _context.TerritoryNames.Add(new TerritoryName
        {
            TerritoryId = 200,
            Name = "Old Name",
            LastUpdatedAt = DateTime.UtcNow.AddDays(-30),
        });
        await _context.SaveChangesAsync();

        var ts = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _controller.UploadPlayers(new List<PostPlayerRequest>
        {
            new() { LocalContentId = 333, Name = "Player", HomeWorldId = 34, TerritoryId = 200, TerritoryName = "New Name", CreatedAt = ts },
        });

        var refreshed = await _context.TerritoryNames.AsNoTracking().FirstAsync(tn => tn.TerritoryId == 200);
        refreshed.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task UploadPlayers_StampsUserScannedPlayerForEachPlayerInBatch()
    {
        var ts = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var batch = new List<PostPlayerRequest>
        {
            new() { LocalContentId = 11, Name = "PlayerA", HomeWorldId = 34, CreatedAt = ts },
            new() { LocalContentId = 22, Name = "PlayerB", HomeWorldId = 65, CreatedAt = ts },
        };

        await _controller.UploadPlayers(batch);

        var rows = await _context.UserScannedPlayers.AsNoTracking()
            .Where(s => s.UserId == _user.Id)
            .ToListAsync();
        rows.Should().HaveCount(2);
        rows.Select(r => r.PlayerLocalContentId).Should().BeEquivalentTo(new long[] { 11, 22 });
    }

    [Fact]
    public async Task UploadPlayers_UpdatesExistingScanRowOnRescan()
    {
        // Same player uploaded twice should result in one row with the LATER timestamp,
        // not two rows. The dashboard query relies on per-(user, player) uniqueness.
        var earlier = (int)DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var first = new List<PostPlayerRequest>
        {
            new() { LocalContentId = 33, Name = "Player33", HomeWorldId = 34, CreatedAt = earlier },
        };
        await _controller.UploadPlayers(first);
        var beforeStamp = await _context.UserScannedPlayers.AsNoTracking()
            .FirstAsync(s => s.UserId == _user.Id && s.PlayerLocalContentId == 33);

        await Task.Delay(20); // ensure UTC clock advances past the previous stamp

        var second = new List<PostPlayerRequest>
        {
            new() { LocalContentId = 33, Name = "Player33", HomeWorldId = 34, CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
        };
        await _controller.UploadPlayers(second);

        var allRows = await _context.UserScannedPlayers.AsNoTracking()
            .Where(s => s.UserId == _user.Id && s.PlayerLocalContentId == 33)
            .ToListAsync();
        allRows.Should().HaveCount(1);
        allRows[0].LastScannedAt.Should().BeAfter(beforeStamp.LastScannedAt);
    }

    [Fact]
    public async Task UploadPlayers_AppendsCustomizationHistoryRowWhenChanged()
    {
        // First scan with all real values: row created.
        var ts = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _controller.UploadPlayers(new List<PostPlayerRequest>
        {
            new()
            {
                LocalContentId = 901, Name = "FantasiaTester", HomeWorldId = 34, CreatedAt = ts,
                Customization = new PlayerCustomization
                {
                    BodyType = 1, GenderRace = 4, Height = 50, Face = 3, SkinColor = 7, Nose = 2,
                    Jaw = 1, MuscleMass = 0, BustSize = 0, TailShape = 0, Mouth = 1, EyeShape = 5, SmallIris = false,
                },
            },
        });

        // Second scan with identical values: no new row (history shouldn't fragment).
        await _controller.UploadPlayers(new List<PostPlayerRequest>
        {
            new()
            {
                LocalContentId = 901, Name = "FantasiaTester", HomeWorldId = 34, CreatedAt = ts + 60,
                Customization = new PlayerCustomization
                {
                    BodyType = 1, GenderRace = 4, Height = 50, Face = 3, SkinColor = 7, Nose = 2,
                    Jaw = 1, MuscleMass = 0, BustSize = 0, TailShape = 0, Mouth = 1, EyeShape = 5, SmallIris = false,
                },
            },
        });

        // Third scan with one byte different (haircut → new Face value): a second row appended.
        await _controller.UploadPlayers(new List<PostPlayerRequest>
        {
            new()
            {
                LocalContentId = 901, Name = "FantasiaTester", HomeWorldId = 34, CreatedAt = ts + 120,
                Customization = new PlayerCustomization
                {
                    BodyType = 1, GenderRace = 4, Height = 50, Face = 9, SkinColor = 7, Nose = 2,
                    Jaw = 1, MuscleMass = 0, BustSize = 0, TailShape = 0, Mouth = 1, EyeShape = 5, SmallIris = false,
                },
            },
        });

        var rows = await _context.PlayerCustomizationHistory.AsNoTracking()
            .Where(h => h.PlayerLocalContentId == 901)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();
        rows.Should().HaveCount(2, "the second identical scan must not create a new row");
        rows[0].Face.Should().Be(3);
        rows[1].Face.Should().Be(9);
    }

    [Fact]
    public async Task UploadPlayers_ReplacesAllNullCustomizationRowOnNextScan()
    {
        // Regression: an early scan from the days when the plugin's PlayerCustomization
        // used [JsonPropertyName] (System.Text.Json) instead of [JsonProperty] (Newtonsoft)
        // would land on the server with every field null. The previous "first scan only"
        // upload logic then permanently blocked any real data from being recorded for
        // that player. Now a real subsequent scan must append a fresh row that the
        // Customization panel can show.
        _context.Players.Add(new Player { LocalContentId = 902, Name = "OldNullRow", HomeWorldId = 34 });
        await _context.SaveChangesAsync();
        _context.PlayerCustomizationHistory.Add(new PlayerCustomizationHistory
        {
            PlayerLocalContentId = 902,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            // every field null
        });
        await _context.SaveChangesAsync();

        await _controller.UploadPlayers(new List<PostPlayerRequest>
        {
            new()
            {
                LocalContentId = 902, Name = "OldNullRow", HomeWorldId = 34,
                CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Customization = new PlayerCustomization
                {
                    BodyType = 2, GenderRace = 6, Height = 30, Face = 1, SkinColor = 5,
                    Nose = 0, Jaw = 0, MuscleMass = 0, BustSize = 0, TailShape = 0,
                    Mouth = 0, EyeShape = 1, SmallIris = true,
                },
            },
        });

        var rows = await _context.PlayerCustomizationHistory.AsNoTracking()
            .Where(h => h.PlayerLocalContentId == 902)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].BodyType.Should().Be(2, "the latest row must carry the real values, not the null stub");
        rows[0].GenderRace.Should().Be(6);
    }

    [Fact]
    public async Task UploadPlayers_RefreshesAccountIdOnSubsequentScans()
    {
        // FFXIV's Character.AccountId can briefly hold a stale value in early
        // game-load before the real id loads. Once a Player row is created with
        // that stale value, every later scan carries the correct id — but the
        // update path used to ignore AccountId, so the row stayed stuck on the
        // wrong value forever and alt-linkage (Player.AccountId match) split
        // characters that should have been grouped.
        var ts = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var staleScan = new List<PostPlayerRequest>
        {
            new() { LocalContentId = 42, Name = "AltSplitter", HomeWorldId = 34, AccountId = 696148207, CreatedAt = ts },
        };
        await _controller.UploadPlayers(staleScan);

        var freshScan = new List<PostPlayerRequest>
        {
            new() { LocalContentId = 42, Name = "AltSplitter", HomeWorldId = 34, AccountId = -441692532, CreatedAt = ts + 60 },
        };
        await _controller.UploadPlayers(freshScan);

        var player = await _context.Players.AsNoTracking().FirstAsync(p => p.LocalContentId == 42);
        player.AccountId.Should().Be(-441692532);
    }

    [Fact]
    public async Task UploadPlayers_DedupesAndIncrementsByUniqueCount()
    {
        // The plugin's outbox can replay duplicate entries for the same player; the
        // controller dedupes to one entry per LocalContentId before processing. The
        // contributions counter should mirror that deduped count so the server-side
        // total stays in step with the plugin-side per-batch counter.
        var ts = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var batch = new List<PostPlayerRequest>
        {
            new() { LocalContentId = 100, Name = "Dup", HomeWorldId = 34, CreatedAt = ts },
            new() { LocalContentId = 100, Name = "Dup", HomeWorldId = 34, CreatedAt = ts },
            new() { LocalContentId = 200, Name = "Once", HomeWorldId = 65, CreatedAt = ts },
        };

        await _controller.UploadPlayers(batch);

        var fresh = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == _user.Id);
        fresh.TotalContributions.Should().Be(2);
    }

}
