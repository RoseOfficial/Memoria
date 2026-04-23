using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using AlphaScopeServer.Services.Lodestone;
using AlphaScopeServer.Tests.TestDoubles;
using TestUtilities;

namespace AlphaScopeServer.Tests.Controllers;

/// <summary>
/// Tests for POST /v1/players/{localContentId}/claim/start.
/// Uses the direct-controller pattern (same as PlayersControllerTests) so there is no
/// middleware, network stack, or WebApplicationFactory DB-isolation puzzle.
/// HttpContext.Items["User"] is set manually to simulate the API-key middleware resolving
/// an authenticated user before the controller action runs.
/// </summary>
public class PlayersClaimTests : IDisposable
{
    private readonly AlphaScopeDbContext _context;
    private readonly ILogger<PlayersController> _mockLogger;
    private readonly PlayersController _controller;
    private readonly ApplicationUser _user;

    public PlayersClaimTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        _context = new AlphaScopeDbContext(options);
        _context.Database.EnsureCreated();

        _mockLogger = LoggerTestUtilities.CreateMockLogger<PlayersController>();
        _controller = new PlayersController(_context, _mockLogger);

        _user = new ApplicationUser
        {
            Name = "TestUser",
            ApiKey = "TESTKEY123",
            PrimaryCharacterLocalContentId = 0
        };
        _context.Users.Add(_user);
        _context.SaveChanges();

        var httpContext = new DefaultHttpContext();
        httpContext.Items["User"] = _user;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    public void Dispose() => _context?.Dispose();

    [Fact]
    public async Task Start_ReturnsCodeAnd24hExpiry()
    {
        var player = new Player { LocalContentId = 1001, Name = "Alt" };
        _context.Players.Add(player);
        _context.SaveChanges();
        _context.PlayerLodestones.Add(new PlayerLodestone { PlayerLocalContentId = 1001, LodestoneId = 777 });
        _context.SaveChanges();

        var result = await _controller.StartClaim(1001);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ClaimStartResponse>().Subject;

        body.Code.Should().StartWith("AS-");
        (body.ExpiresAt - DateTime.UtcNow).TotalHours.Should().BeApproximately(24, 0.1);
        body.Instructions.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Start_Twice_UpsertsAttempt()
    {
        var player = new Player { LocalContentId = 1002, Name = "AltUpsert" };
        _context.Players.Add(player);
        _context.SaveChanges();
        _context.PlayerLodestones.Add(new PlayerLodestone { PlayerLocalContentId = 1002, LodestoneId = 888 });
        _context.SaveChanges();

        var r1 = await _controller.StartClaim(1002);
        var body1 = r1.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<ClaimStartResponse>().Subject;

        var r2 = await _controller.StartClaim(1002);
        var body2 = r2.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<ClaimStartResponse>().Subject;

        // Each call should issue a fresh code.
        body1.Code.Should().StartWith("AS-");
        body2.Code.Should().StartWith("AS-");
        body1.Code.Should().NotBe(body2.Code);

        // Exactly one row in the ClaimAttempts table (upserted, not duplicated).
        var count = await _context.ClaimAttempts
            .CountAsync(a => a.UserId == _user.Id && a.PlayerLocalContentId == 1002);
        count.Should().Be(1);
    }

    [Fact]
    public async Task Start_WithoutLodestoneId_Returns412()
    {
        var player = new Player { LocalContentId = 1003, Name = "NoLodestone" };
        _context.Players.Add(player);
        _context.SaveChanges();
        // No PlayerLodestone row added.

        var result = await _controller.StartClaim(1003);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(412);
    }

    [Fact]
    public async Task Start_PlayerNotFound_Returns404()
    {
        var result = await _controller.StartClaim(9999);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Start_NoUserInContext_Returns401()
    {
        // Simulate a request that bypassed the API-key middleware (HttpContext.Items["User"] is absent).
        var controller = new PlayersController(_context, _mockLogger);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var player = new Player { LocalContentId = 1004, Name = "Public" };
        _context.Players.Add(player);
        _context.SaveChanges();

        var result = await controller.StartClaim(1004);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // -----------------------------------------------------------------------
    // Helpers for verify tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Seeds a Player + PlayerLodestone, creates a ClaimAttempt for _user, and returns a
    /// PlayersController wired with the given bio fetcher and the shared _user HttpContext.
    /// </summary>
    private (PlayersController controller, Player player, ClaimAttempt attempt) CreateVerifyFixture(
        long localContentId,
        int lodestoneId,
        ILodestoneBioFetcher fetcher,
        string code = "AS-TESTCODE",
        int attemptsAlready = 0,
        DateTime? expiresAt = null,
        string playerName = "VerifyAlt",
        short homeWorldId = 74)
    {
        var player = new Player { LocalContentId = localContentId, Name = playerName, HomeWorldId = homeWorldId };
        _context.Players.Add(player);
        _context.SaveChanges();

        _context.PlayerLodestones.Add(new PlayerLodestone
        {
            PlayerLocalContentId = localContentId,
            LodestoneId = lodestoneId
        });

        var attempt = new ClaimAttempt
        {
            UserId = _user.Id,
            PlayerLocalContentId = localContentId,
            Code = code,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(24),
            Attempts = attemptsAlready
        };
        _context.ClaimAttempts.Add(attempt);
        _context.SaveChanges();

        var controller = new PlayersController(_context, _mockLogger);
        var httpContext = new DefaultHttpContext();
        httpContext.Items["User"] = _user;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, player, attempt);
    }

    // -----------------------------------------------------------------------
    // Verify tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Verify_Success_SetsClaimFieldsAndDeletesAttempt()
    {
        const long id = 2001;
        const int lodestoneId = 2001;
        const string code = "AS-VERIFYOK";
        var bio = $"Hello world, my code is {code} yes.";
        var fetcher = new FakeLodestoneBioFetcher(new Dictionary<int, string?> { { lodestoneId, bio } });
        var (controller, _, attempt) = CreateVerifyFixture(id, lodestoneId, fetcher, code);

        var result = await controller.VerifyClaim(id, fetcher, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ClaimVerifyResponse>().Subject;
        body.Claimed.Should().BeTrue();
        body.CharacterName.Should().Be("VerifyAlt");
        body.HomeWorldId.Should().Be(74);
        body.AttemptsLeft.Should().BeNull();

        var player = await _context.Players.FindAsync(id);
        player!.ClaimedByUserId.Should().Be(_user.Id);
        player.ClaimedAt.Should().NotBeNull();
        player.ClaimVerifiedAt.Should().NotBeNull();

        var deleted = await _context.ClaimAttempts.FindAsync(attempt.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task Verify_CaseInsensitiveMatch()
    {
        const long id = 2002;
        const int lodestoneId = 2002;
        const string code = "AS-CASECODE";
        var bio = code.ToLowerInvariant(); // all-lowercase version of the code
        var fetcher = new FakeLodestoneBioFetcher(new Dictionary<int, string?> { { lodestoneId, bio } });
        var (controller, _, _) = CreateVerifyFixture(id, lodestoneId, fetcher, code);

        var result = await controller.VerifyClaim(id, fetcher, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ClaimVerifyResponse>()
            .Which.Claimed.Should().BeTrue();
    }

    [Fact]
    public async Task Verify_WrongCode_IncrementsAttempts()
    {
        const long id = 2003;
        const int lodestoneId = 2003;
        const string code = "AS-MYCODE";
        var fetcher = new FakeLodestoneBioFetcher(new Dictionary<int, string?> { { lodestoneId, "nothing here" } });
        var (controller, _, attempt) = CreateVerifyFixture(id, lodestoneId, fetcher, code, attemptsAlready: 0);

        var result = await controller.VerifyClaim(id, fetcher, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();

        var refreshed = await _context.ClaimAttempts.FindAsync(attempt.Id);
        refreshed.Should().NotBeNull();
        refreshed!.Attempts.Should().Be(1);

        var body = result.Should().BeOfType<BadRequestObjectResult>().Subject
            .Value.Should().BeOfType<ClaimVerifyResponse>().Subject;
        body.Claimed.Should().BeFalse();
        body.AttemptsLeft.Should().Be(4); // 5 - 1
    }

    [Fact]
    public async Task Verify_FifthFailure_Deletes_Returns429()
    {
        const long id = 2004;
        const int lodestoneId = 2004;
        const string code = "AS-FIFTHFAIL";
        var fetcher = new FakeLodestoneBioFetcher(new Dictionary<int, string?> { { lodestoneId, "wrong" } });
        // Already at 4 attempts — the next failure is the 5th.
        var (controller, _, attempt) = CreateVerifyFixture(id, lodestoneId, fetcher, code, attemptsAlready: 4);

        var result = await controller.VerifyClaim(id, fetcher, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(429);

        var deleted = await _context.ClaimAttempts.FindAsync(attempt.Id);
        deleted.Should().BeNull("attempt should be deleted after 5 failures");
    }

    [Fact]
    public async Task Verify_Expired_Returns410AndDeletes()
    {
        const long id = 2005;
        const int lodestoneId = 2005;
        var fetcher = new FakeLodestoneBioFetcher(new Dictionary<int, string?> { { lodestoneId, "AS-EXPIREDCODE" } });
        var pastExpiry = DateTime.UtcNow.AddHours(-1);
        var (controller, _, attempt) = CreateVerifyFixture(id, lodestoneId, fetcher, "AS-EXPIREDCODE", expiresAt: pastExpiry);

        var result = await controller.VerifyClaim(id, fetcher, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(410);

        var deleted = await _context.ClaimAttempts.FindAsync(attempt.Id);
        deleted.Should().BeNull("expired attempt should be deleted");
    }

    [Fact]
    public async Task Verify_TransferPreservesClaimedAt()
    {
        const long id = 2006;
        const int lodestoneId = 2006;
        const string code = "AS-TRANSFER";
        var bio = code;
        var fetcher = new FakeLodestoneBioFetcher(new Dictionary<int, string?> { { lodestoneId, bio } });
        var (controller, player, _) = CreateVerifyFixture(id, lodestoneId, fetcher, code);

        // Pre-claim by another user, with a known ClaimedAt timestamp.
        var originalClaimedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        player.ClaimedByUserId = 999; // some other user id
        player.ClaimedAt = originalClaimedAt;
        await _context.SaveChangesAsync();

        var result = await controller.VerifyClaim(id, fetcher, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<ClaimVerifyResponse>()
            .Which.Claimed.Should().BeTrue();

        var refreshed = await _context.Players.FindAsync(id);
        refreshed!.ClaimedByUserId.Should().Be(_user.Id, "claim transfers to the verifying user");
        refreshed.ClaimedAt.Should().Be(originalClaimedAt, "original ClaimedAt must be preserved");
        refreshed.ClaimVerifiedAt.Should().NotBeNull();
        refreshed.ClaimVerifiedAt.Should().NotBe(originalClaimedAt, "ClaimVerifiedAt is the new timestamp");
    }

    [Fact]
    public async Task Verify_LodestoneFailure_Returns503_DoesNotIncrement()
    {
        const long id = 2007;
        const int lodestoneId = 2007;
        var fetcher = FakeLodestoneBioFetcher.AlwaysFailsWith("timeout");
        var (controller, _, attempt) = CreateVerifyFixture(id, lodestoneId, fetcher, "AS-ANYCODE", attemptsAlready: 2);

        var result = await controller.VerifyClaim(id, fetcher, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);

        var refreshed = await _context.ClaimAttempts.FindAsync(attempt.Id);
        refreshed.Should().NotBeNull();
        refreshed!.Attempts.Should().Be(2, "Lodestone failures must not count against the user");
    }

    [Fact]
    public async Task Verify_NoUserInContext_Returns401()
    {
        var controller = new PlayersController(_context, _mockLogger);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var fetcher = new FakeLodestoneBioFetcher(new Dictionary<int, string?>());

        var result = await controller.VerifyClaim(9999, fetcher, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Verify_NoAttempt_Returns404()
    {
        const long id = 2008;
        const int lodestoneId = 2008;
        var player = new Player { LocalContentId = id, Name = "NoAttemptAlt" };
        _context.Players.Add(player);
        _context.SaveChanges();
        _context.PlayerLodestones.Add(new PlayerLodestone { PlayerLocalContentId = id, LodestoneId = lodestoneId });
        _context.SaveChanges();
        // No ClaimAttempt row seeded.

        var fetcher = new FakeLodestoneBioFetcher(new Dictionary<int, string?>());
        var controller = new PlayersController(_context, _mockLogger);
        var httpContext = new DefaultHttpContext();
        httpContext.Items["User"] = _user;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.VerifyClaim(id, fetcher, CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().Be("Start a claim first.");
    }
}
