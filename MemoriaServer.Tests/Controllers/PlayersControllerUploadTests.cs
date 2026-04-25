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
