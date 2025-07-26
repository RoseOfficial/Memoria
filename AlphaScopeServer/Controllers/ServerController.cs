// ASP.NET Core dependencies
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// AlphaScopeServer internal dependencies
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;

namespace AlphaScopeServer.Controllers
{
    /// <summary>
    /// API controller for server health monitoring and statistics.
    /// Provides endpoints for checking server status, database connectivity,
    /// and retrieving comprehensive server statistics including data counts and performance metrics.
    /// </summary>
    [ApiController]
    [Route("v1/[controller]")]
    public class ServerController : ControllerBase
    {
        /// <summary>
        /// Database context for accessing server data and performing health checks
        /// </summary>
        private readonly AlphaScopeDbContext _context;
        
        /// <summary>
        /// Logger for server monitoring and error tracking
        /// </summary>
        private readonly ILogger<ServerController> _logger;

        /// <summary>
        /// Initializes the ServerController with required dependencies.
        /// </summary>
        /// <param name="context">Database context for health checks and statistics</param>
        /// <param name="logger">Logger for operation tracking</param>
        public ServerController(AlphaScopeDbContext context, ILogger<ServerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Performs a server health check including database connectivity verification.
        /// Returns server status, version information, and current timestamp.
        /// Used by clients to verify server availability and connectivity.
        /// </summary>
        /// <returns>Server status information or error details</returns>
        [HttpGet]
        public async Task<IActionResult> GetServerStatus()
        {
            try
            {
                // Simple health check - verify database connectivity
                await _context.Database.CanConnectAsync();
                return Ok(new { status = "online", version = "v1.2.0", timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Server health check failed");
                return StatusCode(500, new { status = "error", message = "Database connection failed" });
            }
        }

        /// <summary>
        /// Retrieves comprehensive server statistics including player counts, retainer counts,
        /// database metrics, and other operational data.
        /// Used for administrative monitoring and client information displays.
        /// </summary>
        /// <returns>Detailed server statistics or error information</returns>
        [HttpGet("stats")]
        public async Task<ActionResult<ServerStatsDto>> GetServerStats()
        {
            try
            {
                var totalPlayers = await _context.Players.CountAsync();
                var privatePlayers = await _context.Players.CountAsync(p => p.IsPrivate);
                var totalUsers = await _context.Users.CountAsync();

                var stats = new ServerStatsDto
                {
                    TotalPlayerCount = totalPlayers,
                    TotalPrivatePlayerCount = privatePlayers,
                    TotalRetainerCount = 0,
                    TotalPrivateRetainerCount = 0,
                    TotalUserCount = totalUsers,
                    LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting server stats");
                return StatusCode(500, "Error retrieving server statistics");
            }
        }

    }
}