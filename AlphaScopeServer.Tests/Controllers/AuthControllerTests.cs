using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AlphaScopeServer.Controllers;
using AlphaScopeServer.Data;
using TestUtilities;

namespace AlphaScopeServer.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly AlphaScopeDbContext _context;
    private readonly ILogger<AuthController> _mockLogger;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<AlphaScopeDbContext>();
        _context = new AlphaScopeDbContext(options);
        _context.Database.EnsureCreated();

        _mockLogger = LoggerTestUtilities.CreateMockLogger<AuthController>();
        _controller = new AuthController(_context, _mockLogger);
    }

    public void Dispose() => _context?.Dispose();

    // Tests for real endpoints are added in Phase 4 tasks.
}
