using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;

namespace AlphaScopeServer.Controllers
{
    [ApiController]
    [Route("v1/[controller]")]
    public class ServerController : ControllerBase
    {
        private readonly AlphaScopeDbContext _context;
        private readonly ILogger<ServerController> _logger;

        public ServerController(AlphaScopeDbContext context, ILogger<ServerController> logger)
        {
            _context = context;
            _logger = logger;
        }

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

        [HttpGet("stats")]
        public async Task<ActionResult<ServerStatsDto>> GetServerStats()
        {
            try
            {
                var totalPlayers = await _context.Players.CountAsync();
                var privatePlayers = await _context.Players.CountAsync(p => p.IsPrivate);
                var totalRetainers = await _context.Retainers.CountAsync();
                var privateRetainers = await _context.Retainers
                    .CountAsync(r => r.Owner.IsPrivate);
                var totalUsers = await _context.Users.CountAsync();

                var stats = new ServerStatsDto
                {
                    TotalPlayerCount = totalPlayers,
                    TotalPrivatePlayerCount = privatePlayers,
                    TotalRetainerCount = totalRetainers,
                    TotalPrivateRetainerCount = privateRetainers,
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

        [HttpGet("stats/players-retainers")]
        public async Task<ActionResult<ServerPlayerAndRetainerStatsDto>> GetPlayerRetainerStats()
        {
            try
            {
                var playerWorldStats = await _context.Players
                    .Where(p => p.HomeWorldId.HasValue)
                    .GroupBy(p => p.HomeWorldId!.Value)
                    .Select(g => new WorldCountStat
                    {
                        WorldId = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var retainerWorldStats = await _context.Retainers
                    .GroupBy(r => r.WorldId)
                    .Select(g => new WorldCountStat
                    {
                        WorldId = g.Key,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var stats = new ServerPlayerAndRetainerStatsDto
                {
                    PlayerWorldStats = playerWorldStats,
                    RetainerWorldStats = retainerWorldStats,
                    LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player/retainer stats");
                return StatusCode(500, "Error retrieving player/retainer statistics");
            }
        }
    }
}