using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayerScopeServer.Data;
using PlayerScopeServer.Models.DTOs;
using PlayerScopeServer.Models.Entities;

namespace PlayerScopeServer.Controllers
{
    [ApiController]
    [Route("v1/[controller]")]
    public class RetainersController : ControllerBase
    {
        private readonly PlayerScopeDbContext _context;
        private readonly ILogger<RetainersController> _logger;
        private const int PageSize = 25;

        public RetainersController(PlayerScopeDbContext context, ILogger<RetainersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<PaginationBase<RetainerSearchDto>>> SearchRetainers(
            [FromQuery] string? Name = null,
            [FromQuery] int Cursor = 0,
            [FromQuery] bool IsFetching = false,
            [FromQuery] string? F_WorldIds = null,
            [FromQuery] bool? F_MatchAnyPartOfName = false)
        {
            try
            {
                var query = _context.Retainers.Include(r => r.Owner).AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(Name))
                {
                    if (F_MatchAnyPartOfName == true)
                    {
                        query = query.Where(r => r.Name.Contains(Name));
                    }
                    else
                    {
                        query = query.Where(r => r.Name == Name);
                    }
                }

                // Parse world IDs filter
                if (!string.IsNullOrEmpty(F_WorldIds))
                {
                    var worldIds = F_WorldIds.Split(',')
                        .Where(id => short.TryParse(id, out _))
                        .Select(short.Parse)
                        .ToList();
                    
                    if (worldIds.Any())
                    {
                        query = query.Where(r => worldIds.Contains(r.WorldId));
                    }
                }

                // Apply privacy filter (hide retainers of private players unless owned by current user)
                var gameAccountId = HttpContext.Items["GameAccountId"] as int?;
                query = query.Where(r => !r.Owner.IsPrivate || r.Owner.AccountId == gameAccountId);

                // Apply cursor-based pagination
                query = query.Where(r => r.LocalContentId >= Cursor)
                    .OrderBy(r => r.LocalContentId);

                var retainers = await query
                    .Take(PageSize)
                    .Select(r => new RetainerSearchDto
                    {
                        LocalContentId = r.LocalContentId,
                        Name = r.Name,
                        WorldId = (ushort)r.WorldId,
                        OwnerLocalContentId = r.OwnerLocalContentId,
                        CreatedAt = (int)new DateTimeOffset(r.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                    })
                    .ToListAsync();

                // Calculate next cursor and count
                var lastCursor = retainers.LastOrDefault()?.LocalContentId ?? Cursor;
                var remainingCount = await query
                    .Where(r => r.LocalContentId > lastCursor)
                    .CountAsync();

                var result = new PaginationBase<RetainerSearchDto>
                {
                    LastCursor = (int)lastCursor,
                    NextCount = remainingCount,
                    Data = retainers
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching retainers");
                return StatusCode(500, "Error searching retainers");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadRetainers([FromBody] List<PostRetainerRequest> retainers)
        {
            try
            {
                var gameAccountId = HttpContext.Items["GameAccountId"] as int?;
                if (!gameAccountId.HasValue)
                {
                    return Unauthorized("Game account ID not found");
                }

                foreach (var retainerRequest in retainers)
                {
                    // Check if owner exists
                    var owner = await _context.Players
                        .FirstOrDefaultAsync(p => p.LocalContentId == (long)retainerRequest.OwnerLocalContentId);

                    if (owner == null)
                    {
                        _logger.LogWarning("Owner player {OwnerId} not found for retainer {RetainerId}", 
                            retainerRequest.OwnerLocalContentId, retainerRequest.LocalContentId);
                        continue;
                    }

                    var existingRetainer = await _context.Retainers
                        .FirstOrDefaultAsync(r => r.LocalContentId == (long)retainerRequest.LocalContentId);

                    if (existingRetainer == null)
                    {
                        // Create new retainer
                        var newRetainer = new Retainer
                        {
                            LocalContentId = (long)retainerRequest.LocalContentId,
                            Name = retainerRequest.Name ?? string.Empty,
                            WorldId = (short)retainerRequest.WorldId,
                            OwnerLocalContentId = (long)retainerRequest.OwnerLocalContentId,
                            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(retainerRequest.CreatedAt).DateTime,
                            LastSeen = DateTimeOffset.FromUnixTimeSeconds(retainerRequest.CreatedAt).DateTime
                        };

                        _context.Retainers.Add(newRetainer);

                        // Add name history
                        var nameHistory = new RetainerNameHistory
                        {
                            RetainerLocalContentId = newRetainer.LocalContentId,
                            Name = newRetainer.Name,
                            CreatedAt = newRetainer.CreatedAt
                        };
                        _context.RetainerNameHistory.Add(nameHistory);

                        // Add world history
                        var worldHistory = new RetainerWorldHistory
                        {
                            RetainerLocalContentId = newRetainer.LocalContentId,
                            WorldId = newRetainer.WorldId,
                            CreatedAt = newRetainer.CreatedAt
                        };
                        _context.RetainerWorldHistory.Add(worldHistory);
                    }
                    else
                    {
                        // Update existing retainer
                        var hasChanges = false;

                        if (existingRetainer.Name != retainerRequest.Name)
                        {
                            existingRetainer.Name = retainerRequest.Name ?? string.Empty;
                            hasChanges = true;

                            // Add name history entry
                            var nameHistory = new RetainerNameHistory
                            {
                                RetainerLocalContentId = existingRetainer.LocalContentId,
                                Name = existingRetainer.Name,
                                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(retainerRequest.CreatedAt).DateTime
                            };
                            _context.RetainerNameHistory.Add(nameHistory);
                        }

                        if (existingRetainer.WorldId != (short)retainerRequest.WorldId)
                        {
                            existingRetainer.WorldId = (short)retainerRequest.WorldId;
                            hasChanges = true;

                            // Add world history entry
                            var worldHistory = new RetainerWorldHistory
                            {
                                RetainerLocalContentId = existingRetainer.LocalContentId,
                                WorldId = existingRetainer.WorldId,
                                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(retainerRequest.CreatedAt).DateTime
                            };
                            _context.RetainerWorldHistory.Add(worldHistory);
                        }

                        if (hasChanges)
                        {
                            existingRetainer.LastSeen = DateTimeOffset.FromUnixTimeSeconds(retainerRequest.CreatedAt).DateTime;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // Update user stats
                var user = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == gameAccountId.Value);
                if (user != null)
                {
                    user.UploadedRetainersCount += retainers.Count;
                    user.UploadedRetainerInfoCount += retainers.Count;
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Retainers uploaded successfully", count = retainers.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading retainers");
                return StatusCode(500, "Error uploading retainers");
            }
        }
    }
}