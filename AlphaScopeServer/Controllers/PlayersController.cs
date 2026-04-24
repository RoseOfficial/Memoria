// ASP.NET Core dependencies
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// System dependencies
using System.Text.Json;

// AlphaScopeServer internal dependencies
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using AlphaScopeServer.Services.World;

// System dependencies for text processing and HTML parsing
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace AlphaScopeServer.Controllers
{
    /// <summary>
    /// API controller for managing player data in the AlphaScope system.
    /// Provides endpoints for searching players, retrieving detailed player information,
    /// and uploading new player data from the game client plugins.
    /// Supports pagination, filtering, and comprehensive player data management.
    /// </summary>
    [ApiController]
    [Route("v1/[controller]")]
    public class PlayersController : ControllerBase
    {
        /// <summary>
        /// Database context for accessing player and related data
        /// </summary>
        private readonly AlphaScopeDbContext _context;
        
        /// <summary>
        /// Logger for controller operations and error tracking
        /// </summary>
        private readonly ILogger<PlayersController> _logger;
        
        /// <summary>
        /// Number of results to return per page in paginated responses
        /// </summary>
        private const int PageSize = 25;

        /// <summary>
        /// Initializes the PlayersController with required dependencies.
        /// </summary>
        /// <param name="context">Database context for data access</param>
        /// <param name="logger">Logger for operation tracking</param>
        public PlayersController(AlphaScopeDbContext context, ILogger<PlayersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Searches for players based on various criteria with pagination support.
        /// Supports filtering by Content ID, name, world, and provides flexible name matching.
        /// </summary>
        /// <param name="LocalContentId">Specific player Content ID to search for</param>
        /// <param name="Name">Player name to search for</param>
        /// <param name="Cursor">Pagination cursor for retrieving subsequent pages</param>
        /// <param name="IsFetching">Flag indicating if this is a data fetching operation</param>
        /// <param name="F_WorldIds">Comma-separated list of World IDs to filter by</param>
        /// <param name="F_MatchAnyPartOfName">Whether to match partial names (contains) or exact names</param>
        /// <returns>Paginated list of player search results</returns>
        [HttpGet]
        public async Task<ActionResult<PaginationBase<PlayerSearchDto>>> SearchPlayers(
            [FromQuery] long? LocalContentId = null,
            [FromQuery] string? Name = null,
            [FromQuery] int Cursor = 0,
            [FromQuery] bool IsFetching = false,
            [FromQuery] string? F_WorldIds = null,
            [FromQuery] bool? F_MatchAnyPartOfName = false)
        {
            try
            {
                var query = _context.Players.AsQueryable();

                // Apply filters
                if (LocalContentId.HasValue)
                {
                    query = query.Where(p => p.LocalContentId == LocalContentId.Value);
                }

                if (!string.IsNullOrEmpty(Name))
                {
                    if (F_MatchAnyPartOfName == true)
                    {
                        query = query.Where(p => p.Name.Contains(Name));
                    }
                    else
                    {
                        query = query.Where(p => p.Name == Name);
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
                        query = query.Where(p => p.CurrentWorldId.HasValue && worldIds.Contains(p.CurrentWorldId.Value));
                    }
                }

                // Privacy filter removed - public API shows all players

                // Apply cursor-based pagination
                query = query.Where(p => p.LocalContentId >= Cursor)
                    .OrderBy(p => p.LocalContentId);

                var players = await query
                    .Take(PageSize)
                    .Select(p => new PlayerSearchDto
                    {
                        LocalContentId = p.LocalContentId,
                        Name = p.Name,
                        WorldId = p.CurrentWorldId,
                        AccountId = p.AccountId,
                        AvatarLink = p.AvatarLink,
                        CurrentJobId = p.CurrentJobId,
                        CurrentJobLevel = p.CurrentJobLevel,
                        HomeWorldId = p.HomeWorldId,
                        LastScannedAt = p.LastScannedAt,
                        LodestoneJobData = p.LodestoneJobData,
                        MainJobId = p.MainJobId,
                        MainJobLevel = p.MainJobLevel,
                        LastJobDataUpdate = p.LastJobDataUpdate,
                        LodestoneMinionsData = p.LodestoneMinionsData,
                        LastMinionsDataUpdate = p.LastMinionsDataUpdate,
                        LodestoneMountsData = p.LodestoneMountsData,
                        LastMountsDataUpdate = p.LastMountsDataUpdate,
                    })
                    .ToListAsync();

                // Calculate next cursor and count
                var lastCursor = players.LastOrDefault()?.LocalContentId ?? Cursor;
                var remainingCount = await query
                    .Where(p => p.LocalContentId > lastCursor)
                    .CountAsync();

                var result = new PaginationBase<PlayerSearchDto>
                {
                    LastCursor = (int)lastCursor,
                    NextCount = remainingCount,
                    Data = players
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching players");
                return StatusCode(500, "Error searching players");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PlayerDetailed>> GetPlayerById(long id)
        {
            try
            {
                var player = await _context.Players
                    .Include(p => p.NameHistory)
                    .Include(p => p.WorldHistory)
                    .Include(p => p.CustomizationHistory)
                    .Include(p => p.TerritoryHistory)
                    .Include(p => p.Lodestone)
                    .Include(p => p.ProfileVisits)
                    .FirstOrDefaultAsync(p => p.LocalContentId == id);

                if (player == null)
                {
                    return NotFound("Player not found");
                }

                // Privacy check removed - public API allows viewing all players

                // Map to detailed DTO
                var detailed = new PlayerDetailed
                {
                    Flags = [], // TODO: Implement flags system
                    LocalContentId = player.LocalContentId,
                    AccountId = player.AccountId,
                    PlayerCustomizationHistories = player.CustomizationHistory
                        .OrderByDescending(h => h.CreatedAt)
                        .Select(h => new PlayerCustomizationHistoryDto
                        {
                            BodyType = h.BodyType,
                            GenderRace = h.GenderRace,
                            Height = h.Height,
                            Face = h.Face,
                            SkinColor = h.SkinColor,
                            Nose = h.Nose,
                            Jaw = h.Jaw,
                            MuscleMass = h.MuscleMass,
                            BustSize = h.BustSize,
                            TailShape = h.TailShape,
                            Mouth = h.Mouth,
                            EyeShape = h.EyeShape,
                            SmallIris = h.SmallIris,
                            CreatedAt = (int)new DateTimeOffset(h.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                        }).ToList(),
                    TerritoryHistory = player.TerritoryHistory
                        .OrderByDescending(t => t.LastSeenAt)
                        .Select(t => new PlayerTerritoryHistoryDto
                        {
                            TerritoryId = t.TerritoryId,
                            PlayerPos = t.PlayerPos,
                            WorldId = t.WorldId,
                            FirstSeenAt = (int)new DateTimeOffset(t.FirstSeenAt, TimeSpan.Zero).ToUnixTimeSeconds(),
                            LastSeenAt = (int)new DateTimeOffset(t.LastSeenAt, TimeSpan.Zero).ToUnixTimeSeconds()
                        }).ToList(),
                    PlayerLodestone = player.Lodestone != null ? new PlayerLodestoneDto
                    {
                        LodestoneId = player.Lodestone.LodestoneId,
                        CharacterCreationDate = player.Lodestone.CharacterCreationDate.HasValue 
                            ? (int)new DateTimeOffset(player.Lodestone.CharacterCreationDate.Value, TimeSpan.Zero).ToUnixTimeSeconds() 
                            : null,
                        AvatarLink = player.Lodestone.AvatarLink
                    } : null,
                    PlayerNameHistories = player.NameHistory
                        .OrderByDescending(n => n.CreatedAt)
                        .Select(n => new PlayerNameHistoryDto
                        {
                            Name = n.Name,
                            CreatedAt = (int)new DateTimeOffset(n.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                        }).ToList(),
                    PlayerWorldHistories = player.WorldHistory
                        .OrderByDescending(w => w.CreatedAt)
                        .Select(w => new PlayerWorldHistoryDto
                        {
                            WorldId = w.WorldId,
                            CreatedAt = (int)new DateTimeOffset(w.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                        }).ToList(),
                    PlayerAltCharacters = [], // TODO: Implement alt characters
                    ProfileVisitInfo = new PlayerProfileVisitInfoDto
                    {
                        ProfileTotalVisitCount = player.ProfileVisits.Count,
                        LastProfileVisitDate = player.ProfileVisits.Any() 
                            ? (int)new DateTimeOffset(player.ProfileVisits.Max(v => v.VisitedAt), TimeSpan.Zero).ToUnixTimeSeconds()
                            : 0,
                        UniqueVisitorCount = player.ProfileVisits.Select(v => v.VisitorId).Distinct().Count()
                    },
                    CurrentJobId = player.CurrentJobId,
                    CurrentJobLevel = player.CurrentJobLevel
                };

                // Profile visit tracking removed - public API

                return Ok(detailed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player {PlayerId}", id);
                return StatusCode(500, "Error retrieving player details");
            }
        }

        [HttpGet("recent")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRecent()
        {
            var raw = await _context.Players
                .Where(p => !p.HideEntirely && !p.IsPrivate && !p.HideInSearch && p.LastScannedAt != null)
                .OrderByDescending(p => p.LastScannedAt)
                .Take(20)
                .Select(p => new { p.Name, p.HomeWorldId, p.AvatarLink, p.LastScannedAt })
                .ToListAsync();

            var items = raw.Select(p => new RecentPlayerItem(
                p.Name,
                p.HomeWorldId.HasValue ? WorldNames.ToSlug(WorldNames.Resolve(p.HomeWorldId) ?? "unknown") : "unknown",
                p.HomeWorldId.HasValue ? WorldNames.Resolve(p.HomeWorldId) ?? "Unknown" : "Unknown",
                p.AvatarLink,
                p.LastScannedAt!.Value)).ToList();

            return Ok(new RecentPlayerResponse(items));
        }

        [HttpGet("by-slug")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBySlug([FromQuery] string world, [FromQuery] string name)
        {
            var worldId = WorldNames.ResolveFromSlug(world);
            if (worldId is null) return NotFound();

            var nameLower = name.ToLowerInvariant();

            // Filter by worldId in SQL (uses index), slug-match on Name in memory
            var candidates = await _context.Players
                .Where(p => p.HomeWorldId == worldId)
                .ToListAsync();
            var player = candidates.FirstOrDefault(p => WorldNames.ToSlug(p.Name) == nameLower);

            if (player is null)
            {
                // History fallback: search NameHistory and WorldHistory for any match.
                // NameHistory entries are keyed to a Player whose CURRENT name may differ.
                // Use the same worldId-filter + client-side slug match approach as the primary lookup.
                var nameHistoryCandidates = await _context.Set<PlayerNameHistory>()
                    .Include(h => h.Player)
                    .Where(h => h.Player != null && h.Player.HomeWorldId == worldId)
                    .ToListAsync();
                var historicByName = nameHistoryCandidates
                    .FirstOrDefault(h => WorldNames.ToSlug(h.Name) == nameLower)?.Player;

                if (historicByName != null)
                    return RedirectToCanonical(historicByName);

                var worldHistoryCandidates = await _context.Set<PlayerWorldHistory>()
                    .Include(h => h.Player)
                    .Where(h => h.WorldId == worldId)
                    .ToListAsync();
                var historicByWorld = worldHistoryCandidates
                    .FirstOrDefault(h => h.Player != null && WorldNames.ToSlug(h.Player.Name) == nameLower)?.Player;

                if (historicByWorld != null)
                    return RedirectToCanonical(historicByWorld);

                return NotFound();
            }

            var viewerUserId = HttpContext.Items["ViewerUserId"] as int?;
            var isOwner = viewerUserId.HasValue && player.ClaimedByUserId == viewerUserId.Value;
            var isAdmin = (bool)(HttpContext.Items["IsAdmin"] ?? false);

            if (player.HideEntirely && !isOwner && !isAdmin)
                return NotFound();

            var tier = (int)(HttpContext.Items["Tier"] ?? 1);
            var dto = BuildProfileResponse(player, tier, isOwner);
            return Ok(dto);
        }

        private IActionResult RedirectToCanonical(Player player)
        {
            var worldSlug = WorldNames.ToSlug(WorldNames.Resolve(player.HomeWorldId) ?? "unknown");
            var nameSlug = WorldNames.ToSlug(player.Name);
            return RedirectPermanent($"/p/{worldSlug}/{nameSlug}");
        }

        private PlayerProfileResponse BuildProfileResponse(Player player, int tier, bool isOwner)
        {
            var worldName = WorldNames.Resolve(player.HomeWorldId) ?? "Unknown";
            var worldSlug = WorldNames.ToSlug(worldName);

            var header = new ProfileHeader(
                LocalContentId: player.LocalContentId,
                Name: player.Name,
                WorldSlug: worldSlug,
                WorldName: worldName,
                AvatarUrl: player.AvatarLink,
                CurrentJobId: player.CurrentJobId,
                CurrentJobLevel: player.CurrentJobLevel,
                FreeCompanyTag: null,  // not tracked at base Player level yet
                LastSeenAt: player.LastScannedAt,
                LastSeenTerritory: null,
                FirstScannedAt: player.CreatedAt);

            // Tier 1 sections — always filled if data exists
            var jobs = BuildJobs(player);
            var customization = BuildCustomization(player);
            var mounts = BuildMounts(player);
            var minions = BuildMinions(player);

            // Tier 2+ sections: null for anon/below-tier viewers
            LocationsData? locations = null;
            IReadOnlyList<NameHistoryEntry>? nameHistory = null;
            IReadOnlyList<WorldHistoryEntry>? worldHistory = null;
            IReadOnlyList<AltCharacter>? alts = null;
            // These get populated in a later task when Tier 2 rendering is enabled end-to-end.

            return new PlayerProfileResponse(header, jobs, customization, mounts, minions,
                locations, nameHistory, worldHistory, alts, isOwner);
        }

        private CustomizationData? BuildCustomization(Player player)
        {
            var latest = _context.Set<PlayerCustomizationHistory>()
                .Where(c => c.PlayerLocalContentId == player.LocalContentId)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefault();
            if (latest is null) return null;
            return new CustomizationData(
                latest.BodyType, latest.GenderRace, latest.Height, latest.Face,
                latest.SkinColor, latest.Nose, latest.Jaw, latest.EyeShape);
        }

        private static MountsData? BuildMounts(Player player)
        {
            var build = BuildCollectibles(player.LodestoneMountsData, iconBase: "https://ffxivcollect.com/icons/mounts/", kind: "mount");
            return build is null ? null : new MountsData(build.Collected, build.KnownTotal, build.Preview);
        }

        private static MinionsData? BuildMinions(Player player)
        {
            var build = BuildCollectibles(player.LodestoneMinionsData, iconBase: "https://ffxivcollect.com/icons/minions/", kind: "minion");
            return build is null ? null : new MinionsData(build.Collected, build.KnownTotal, build.Preview);
        }

        private sealed record CollectibleBuild(int Collected, int KnownTotal, IReadOnlyList<CollectibleIcon> Preview);

        private static CollectibleBuild? BuildCollectibles(string? json, string iconBase, string kind)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var names = JsonSerializer.Deserialize<List<string>>(json);
                if (names is null) return null;
                // KnownTotal: ~400 for mounts, ~480 for minions per FFXIVCollect. Hard-coded estimates.
                var total = kind == "mount" ? 400 : 480;
                var preview = names.Take(16).Select((n, i) =>
                    new CollectibleIcon(i, n, iconBase + Uri.EscapeDataString(n.ToLowerInvariant().Replace(" ", "-")) + ".png"))
                    .ToList();
                return new CollectibleBuild(names.Count, total, preview);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static JobsData BuildJobs(Player player)
        {
            if (string.IsNullOrWhiteSpace(player.LodestoneJobData))
                return new JobsData(Array.Empty<JobEntry>());

            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, short>>(player.LodestoneJobData);
                if (parsed is null) return new JobsData(Array.Empty<JobEntry>());
                return new JobsData(parsed
                    .Where(kvp => kvp.Value > 0)
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => new JobEntry(kvp.Key, kvp.Value))
                    .ToList());
            }
            catch (JsonException)
            {
                return new JobsData(Array.Empty<JobEntry>());
            }
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Patch(long id, [FromBody] PlayerPrivacyPatchRequest body)
        {
            var viewerUserId = HttpContext.Items["ViewerUserId"] as int?;
            if (viewerUserId is null) return Unauthorized();

            var player = await _context.Players.FindAsync(id);
            if (player is null || player.ClaimedByUserId != viewerUserId.Value)
                return NotFound();

            if (body.HideAlts is { } a) player.HideAlts = a;
            if (body.HideEncounters is { } e) player.HideEncounters = e;
            if (body.HideEntirely is { } h) player.HideEntirely = h;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> UploadPlayers([FromBody] List<PostPlayerRequest> players)
        {
            try
            {
                // Dedupe by LocalContentId before the loop. The plugin's outbox can replay hundreds
                // of pending snapshots and they frequently include multiple entries for the same
                // player; processing them naively double-Adds the entity and EF's change tracker
                // throws "already being tracked". Keep the newest snapshot per player.
                players = players
                    .Where(p => p != null && p.LocalContentId != 0)
                    .GroupBy(p => p.LocalContentId)
                    .Select(g => g.OrderByDescending(p => p.CreatedAt).First())
                    .ToList();

                foreach (var playerRequest in players)
                {
                    var existingPlayer = await _context.Players
                        .FirstOrDefaultAsync(p => p.LocalContentId == (long)playerRequest.LocalContentId);

                    if (existingPlayer == null)
                    {
                        // Create new player
                        var newPlayer = new Player
                        {
                            LocalContentId = (long)playerRequest.LocalContentId,
                            Name = playerRequest.Name,
                            AccountId = playerRequest.AccountId,
                            HomeWorldId = (short?)playerRequest.HomeWorldId,
                            CurrentWorldId = (short?)playerRequest.CurrentWorldId,
                            TerritoryId = playerRequest.TerritoryId,
                            CurrentJobId = playerRequest.CurrentJobId,
                            CurrentJobLevel = playerRequest.CurrentJobLevel,
                            PlayerPos = playerRequest.PlayerPos,
                            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).UtcDateTime,
                            LodestoneJobData = playerRequest.LodestoneJobData,
                            MainJobId = playerRequest.MainJobId,
                            MainJobLevel = playerRequest.MainJobLevel,
                            LastJobDataUpdate = playerRequest.LastJobDataUpdate,
                            LodestoneMinionsData = playerRequest.LodestoneMinionsData,
                            LastMinionsDataUpdate = playerRequest.LastMinionsDataUpdate,
                            LodestoneMountsData = playerRequest.LodestoneMountsData,
                            LastMountsDataUpdate = playerRequest.LastMountsDataUpdate
                        };

                        _context.Players.Add(newPlayer);
                    }
                    else
                    {
                        // Update existing player
                        var hasChanges = false;

                        if (existingPlayer.Name != playerRequest.Name)
                        {
                            existingPlayer.Name = playerRequest.Name;
                            hasChanges = true;

                            // Add name history entry
                            var nameHistory = new PlayerNameHistory
                            {
                                PlayerLocalContentId = existingPlayer.LocalContentId,
                                Name = playerRequest.Name,
                                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).UtcDateTime
                            };
                            _context.PlayerNameHistory.Add(nameHistory);
                        }

                        if (existingPlayer.CurrentWorldId != (short?)playerRequest.CurrentWorldId)
                        {
                            existingPlayer.CurrentWorldId = (short?)playerRequest.CurrentWorldId;
                            hasChanges = true;

                            if (playerRequest.CurrentWorldId.HasValue)
                            {
                                var worldHistory = new PlayerWorldHistory
                                {
                                    PlayerLocalContentId = existingPlayer.LocalContentId,
                                    WorldId = (short)playerRequest.CurrentWorldId.Value,
                                    CreatedAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).UtcDateTime
                                };
                                _context.PlayerWorldHistory.Add(worldHistory);
                            }
                        }

                        if (existingPlayer.CurrentJobId != playerRequest.CurrentJobId || 
                            existingPlayer.CurrentJobLevel != playerRequest.CurrentJobLevel)
                        {
                            existingPlayer.CurrentJobId = playerRequest.CurrentJobId;
                            existingPlayer.CurrentJobLevel = playerRequest.CurrentJobLevel;
                            hasChanges = true;
                        }

                        if (existingPlayer.TerritoryId != playerRequest.TerritoryId || 
                            existingPlayer.PlayerPos != playerRequest.PlayerPos)
                        {
                            existingPlayer.TerritoryId = playerRequest.TerritoryId;
                            existingPlayer.PlayerPos = playerRequest.PlayerPos;
                            hasChanges = true;

                            // Update or create territory history
                            // Explicit (short?) cast is required: t.WorldId is short (smallint) but
                            // CurrentWorldId is ushort?, and Npgsql refuses to bind a UInt16
                            // parameter as smallint.
                            var territoryHistory = await _context.PlayerTerritoryHistory
                                .FirstOrDefaultAsync(t => t.PlayerLocalContentId == existingPlayer.LocalContentId &&
                                                        t.TerritoryId == playerRequest.TerritoryId &&
                                                        t.WorldId == (short?)playerRequest.CurrentWorldId);

                            if (territoryHistory == null && playerRequest.CurrentWorldId.HasValue)
                            {
                                // Only record territory history if we actually have a world id;
                                // otherwise the (short) cast on CurrentWorldId!.Value would NRE.
                                territoryHistory = new PlayerTerritoryHistory
                                {
                                    PlayerLocalContentId = existingPlayer.LocalContentId,
                                    TerritoryId = playerRequest.TerritoryId,
                                    PlayerPos = playerRequest.PlayerPos,
                                    WorldId = (short)playerRequest.CurrentWorldId.Value,
                                    FirstSeenAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).UtcDateTime,
                                    LastSeenAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).UtcDateTime
                                };
                                _context.PlayerTerritoryHistory.Add(territoryHistory);
                            }
                            else
                            {
                                territoryHistory.LastSeenAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).UtcDateTime;
                                territoryHistory.PlayerPos = playerRequest.PlayerPos;
                            }
                        }

                        // Handle Lodestone job data updates
                        if (existingPlayer.LodestoneJobData != playerRequest.LodestoneJobData ||
                            existingPlayer.MainJobId != playerRequest.MainJobId ||
                            existingPlayer.MainJobLevel != playerRequest.MainJobLevel ||
                            existingPlayer.LastJobDataUpdate != playerRequest.LastJobDataUpdate)
                        {
                            existingPlayer.LodestoneJobData = playerRequest.LodestoneJobData;
                            existingPlayer.MainJobId = playerRequest.MainJobId;
                            existingPlayer.MainJobLevel = playerRequest.MainJobLevel;
                            existingPlayer.LastJobDataUpdate = playerRequest.LastJobDataUpdate;
                            hasChanges = true;
                        }

                        // Handle Lodestone minions data updates
                        if (existingPlayer.LodestoneMinionsData != playerRequest.LodestoneMinionsData ||
                            existingPlayer.LastMinionsDataUpdate != playerRequest.LastMinionsDataUpdate)
                        {
                            existingPlayer.LodestoneMinionsData = playerRequest.LodestoneMinionsData;
                            existingPlayer.LastMinionsDataUpdate = playerRequest.LastMinionsDataUpdate;
                            hasChanges = true;
                        }

                        // Handle Lodestone mounts data updates
                        if (existingPlayer.LodestoneMountsData != playerRequest.LodestoneMountsData ||
                            existingPlayer.LastMountsDataUpdate != playerRequest.LastMountsDataUpdate)
                        {
                            existingPlayer.LodestoneMountsData = playerRequest.LodestoneMountsData;
                            existingPlayer.LastMountsDataUpdate = playerRequest.LastMountsDataUpdate;
                            hasChanges = true;
                        }

                        if (hasChanges)
                        {
                            existingPlayer.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // Add history entries for new players
                foreach (var playerRequest in players)
                {
                    var player = await _context.Players
                        .FirstOrDefaultAsync(p => p.LocalContentId == (long)playerRequest.LocalContentId);
                    
                    if (player != null)
                    {
                        // Check if name history already exists
                        var hasNameHistory = await _context.PlayerNameHistory
                            .AnyAsync(h => h.PlayerLocalContentId == player.LocalContentId && h.Name == player.Name);
                        
                        if (!hasNameHistory)
                        {
                            var nameHistory = new PlayerNameHistory
                            {
                                PlayerLocalContentId = player.LocalContentId,
                                Name = player.Name,
                                CreatedAt = player.CreatedAt
                            };
                            _context.PlayerNameHistory.Add(nameHistory);
                        }

                        // Add customization history if provided and doesn't exist
                        if (playerRequest.Customization != null)
                        {
                            var hasCustomizationHistory = await _context.PlayerCustomizationHistory
                                .AnyAsync(h => h.PlayerLocalContentId == player.LocalContentId);
                            
                            if (!hasCustomizationHistory)
                            {
                                var customization = new PlayerCustomizationHistory
                                {
                                    PlayerLocalContentId = player.LocalContentId,
                                    BodyType = playerRequest.Customization.BodyType,
                                    GenderRace = playerRequest.Customization.GenderRace,
                                    Height = playerRequest.Customization.Height,
                                    Face = playerRequest.Customization.Face,
                                    SkinColor = playerRequest.Customization.SkinColor,
                                    Nose = playerRequest.Customization.Nose,
                                    Jaw = playerRequest.Customization.Jaw,
                                    MuscleMass = playerRequest.Customization.MuscleMass,
                                    BustSize = playerRequest.Customization.BustSize,
                                    TailShape = playerRequest.Customization.TailShape,
                                    Mouth = playerRequest.Customization.Mouth,
                                    EyeShape = playerRequest.Customization.EyeShape,
                                    SmallIris = playerRequest.Customization.SmallIris,
                                    CreatedAt = player.CreatedAt
                                };
                                _context.PlayerCustomizationHistory.Add(customization);
                            }
                        }
                    }
                }

                // Save history entries
                await _context.SaveChangesAsync();

                // Schedule auto-linking for players without avatars (rate-limited background task)
                var playersNeedingAvatars = players.Where(p => p.LocalContentId != 0).Take(5).ToList(); // Limit to 5 players per batch
                if (playersNeedingAvatars.Any())
                {
                    var serviceScope = HttpContext.RequestServices.CreateScope();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = serviceScope;
                            var scopedContext = scope.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();
                            var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<PlayersController>>();
                            
                            foreach (var playerRequest in playersNeedingAvatars)
                            {
                                var player = await scopedContext.Players
                                    .FirstOrDefaultAsync(p => p.LocalContentId == (long)playerRequest.LocalContentId);
                                
                                if (player != null && string.IsNullOrEmpty(player.AvatarLink))
                                {
                                    await AutoLinkLodestoneForPlayer(player, scopedContext, scopedLogger);
                                    await scopedContext.SaveChangesAsync();
                                    
                                    // Rate limiting: 5 seconds between requests
                                    await Task.Delay(5000);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error in background Lodestone auto-linking");
                        }
                    });
                }

                // User stats tracking removed - public API

                return Ok(new { message = "Players uploaded successfully", count = players.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading players");
                return StatusCode(500, new
                {
                    error = "Error uploading players",
                    type = ex.GetType().Name,
                    detail = ex.Message,
                    inner = ex.InnerException?.Message,
                });
            }
        }

        private async Task<int?> SearchLodestoneCharacter(string characterName, string worldName, ILogger<PlayersController> logger)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                // Lodestone character search URL
                var searchUrl = $"https://na.finalfantasyxiv.com/lodestone/character/?q={Uri.EscapeDataString(characterName)}&worldname={Uri.EscapeDataString(worldName)}";
                logger.LogInformation($"Searching Lodestone for character: {characterName} on {worldName}");

                var response = await httpClient.GetStringAsync(searchUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(response);

                // Find character entries in search results
                var characterEntries = doc.DocumentNode.SelectNodes("//div[@class='entry']");
                
                if (characterEntries == null || !characterEntries.Any())
                {
                    logger.LogInformation($"No search results found for {characterName} on {worldName}");
                    return null;
                }

                // Look for exact name match
                foreach (var entry in characterEntries)
                {
                    var nameNode = entry.SelectSingleNode(".//p[@class='entry__name']");
                    var worldNode = entry.SelectSingleNode(".//p[@class='entry__world']");
                    var linkNode = entry.SelectSingleNode(".//a[@class='entry__link']");

                    if (nameNode != null && worldNode != null && linkNode != null)
                    {
                        var foundName = nameNode.InnerText.Trim();
                        var foundWorld = worldNode.InnerText.Trim();

                        // Check for exact match (world name may include data center, e.g., "Famfrit [Primal]")
                        var foundWorldName = foundWorld.Split('[')[0].Trim(); // Extract world name without data center
                        if (string.Equals(foundName, characterName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(foundWorldName, worldName, StringComparison.OrdinalIgnoreCase))
                        {
                            var href = linkNode.GetAttributeValue("href", "");
                            var match = Regex.Match(href, @"/lodestone/character/(\d+)/");
                            
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int lodestoneId))
                            {
                                logger.LogInformation($"Found exact match for {characterName}@{worldName}: Lodestone ID {lodestoneId}");
                                return lodestoneId;
                            }
                        }
                    }
                }

                logger.LogInformation($"No exact match found for {characterName}@{worldName}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error searching Lodestone for character {characterName}@{worldName}");
                return null;
            }
        }

        private async Task<(string NameAndWorld, string AvatarLink)?> ScrapeLodestoneProfile(int lodestoneId, ILogger<PlayersController> logger)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                var url = $"https://na.finalfantasyxiv.com/lodestone/character/{lodestoneId}/";
                var response = await httpClient.GetStringAsync(url);

                var doc = new HtmlDocument();
                doc.LoadHtml(response);

                // Extract character name and world
                var nameElement = doc.DocumentNode.SelectSingleNode("//p[@class='frame__chara__name']");
                var worldElement = doc.DocumentNode.SelectSingleNode("//p[@class='frame__chara__world']");
                
                if (nameElement == null || worldElement == null)
                {
                    logger.LogWarning($"Could not find name/world elements for Lodestone ID {lodestoneId}");
                    return null;
                }

                var characterName = nameElement.InnerText.Trim();
                var worldName = worldElement.InnerText.Trim();
                var nameAndWorld = $"{characterName}@{worldName}";

                // Extract avatar URL
                var avatarElement = doc.DocumentNode.SelectSingleNode("//div[@class='frame__chara__face']//img");
                var avatarUrl = string.Empty;

                if (avatarElement != null)
                {
                    var src = avatarElement.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src))
                    {
                        // Convert to full URL if relative
                        if (src.StartsWith("/"))
                        {
                            avatarUrl = $"https://img2.finalfantasyxiv.com{src}";
                        }
                        else
                        {
                            avatarUrl = src;
                        }
                    }
                }

                logger.LogInformation($"Scraped Lodestone profile {lodestoneId}: {nameAndWorld}, Avatar: {avatarUrl}");
                
                return (nameAndWorld, avatarUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error scraping Lodestone profile {lodestoneId}");
                return null;
            }
        }

        private async Task<bool> AutoLinkLodestoneForPlayer(Player player, AlphaScopeDbContext context, ILogger<PlayersController> logger)
        {
            try
            {
                // Skip if player already has Lodestone data
                if (!string.IsNullOrEmpty(player.AvatarLink))
                {
                    return false;
                }

                // Try searching with both home world and current world
                int? lodestoneId = null;
                
                // First try with HomeWorldId if available
                if (player.HomeWorldId.HasValue)
                {
                    var homeWorldName = WorldNames.Resolve(player.HomeWorldId);
                    if (!string.IsNullOrEmpty(homeWorldName))
                    {
                        lodestoneId = await SearchLodestoneCharacter(player.Name, homeWorldName, logger);
                    }
                }
                
                // If not found and CurrentWorldId is different, try current world
                if (!lodestoneId.HasValue && player.CurrentWorldId.HasValue && player.CurrentWorldId != player.HomeWorldId)
                {
                    var currentWorldName = WorldNames.Resolve(player.CurrentWorldId);
                    if (!string.IsNullOrEmpty(currentWorldName))
                    {
                        lodestoneId = await SearchLodestoneCharacter(player.Name, currentWorldName, logger);
                    }
                }
                
                // If still not found, log the issue and return
                if (!lodestoneId.HasValue)
                {
                    logger.LogInformation($"Could not find {player.Name} on Lodestone (tried Home: {WorldNames.Resolve(player.HomeWorldId)}, Current: {WorldNames.Resolve(player.CurrentWorldId)})");
                    return false;
                }

                // Scrape the character profile
                var profileData = await ScrapeLodestoneProfile(lodestoneId.Value, logger);
                if (profileData == null)
                {
                    return false;
                }

                // Create or update PlayerLodestone record
                var existingLodestone = await context.PlayerLodestones
                    .FirstOrDefaultAsync(pl => pl.PlayerLocalContentId == player.LocalContentId);

                if (existingLodestone == null)
                {
                    var newPlayerLodestone = new PlayerLodestone
                    {
                        PlayerLocalContentId = player.LocalContentId,
                        LodestoneId = lodestoneId.Value,
                        AvatarLink = profileData.Value.AvatarLink
                    };
                    context.PlayerLodestones.Add(newPlayerLodestone);
                }
                else
                {
                    existingLodestone.LodestoneId = lodestoneId.Value;
                    existingLodestone.AvatarLink = profileData.Value.AvatarLink;
                }

                // Update player's direct avatar link
                player.AvatarLink = profileData.Value.AvatarLink;

                logger.LogInformation($"Auto-linked player {player.Name} to Lodestone ID {lodestoneId.Value} with avatar: {profileData.Value.AvatarLink}");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error auto-linking Lodestone for player {player.Name}");
                return false;
            }
        }

        [HttpPost("{localContentId:long}/claim/start")]
        public async Task<IActionResult> StartClaim(long localContentId)
        {
            if (HttpContext.Items["User"] is not ApplicationUser user)
                return Unauthorized();

            var player = await _context.Players
                .Include(p => p.Lodestone)
                .FirstOrDefaultAsync(p => p.LocalContentId == localContentId);
            if (player is null)
                return NotFound();
            if (player.Lodestone?.LodestoneId is null)
                return StatusCode(412,
                    "This character hasn't been linked to a Lodestone profile yet. Once the profile is resolved you can begin the claim.");

            var existing = await _context.ClaimAttempts
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.PlayerLocalContentId == localContentId);
            if (existing is null)
            {
                existing = new ClaimAttempt
                {
                    UserId = user.Id,
                    PlayerLocalContentId = localContentId,
                    Code = AlphaScopeServer.Services.Auth.ClaimCodeGenerator.GenerateClaimCode(),
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    Attempts = 0,
                };
                _context.ClaimAttempts.Add(existing);
            }
            else
            {
                existing.Code = AlphaScopeServer.Services.Auth.ClaimCodeGenerator.GenerateClaimCode();
                existing.ExpiresAt = DateTime.UtcNow.AddHours(24);
                existing.Attempts = 0;
            }
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Concurrent insert by the same user — pick up the winner's row and return it.
                existing = await _context.ClaimAttempts
                    .AsNoTracking()
                    .FirstAsync(a => a.UserId == user.Id && a.PlayerLocalContentId == localContentId);
            }

            return Ok(new ClaimStartResponse(
                existing.Code,
                existing.ExpiresAt,
                "Paste this code anywhere in your Lodestone character's bio, then return here and click Verify."));
        }

        [HttpPost("{localContentId:long}/claim/verify")]
        public async Task<IActionResult> VerifyClaim(
            long localContentId,
            [FromServices] AlphaScopeServer.Services.Lodestone.ILodestoneBioFetcher bioFetcher,
            CancellationToken ct)
        {
            if (HttpContext.Items["User"] is not ApplicationUser user)
                return Unauthorized();

            var attempt = await _context.ClaimAttempts
                .FirstOrDefaultAsync(a => a.UserId == user.Id && a.PlayerLocalContentId == localContentId, ct);
            if (attempt is null)
                return NotFound("Start a claim first.");

            if (attempt.ExpiresAt < DateTime.UtcNow)
            {
                _context.ClaimAttempts.Remove(attempt);
                await _context.SaveChangesAsync(ct);
                return StatusCode(StatusCodes.Status410Gone, "Code expired — start again.");
            }

            var player = await _context.Players
                .Include(p => p.Lodestone)
                .FirstOrDefaultAsync(p => p.LocalContentId == localContentId, ct);
            if (player?.Lodestone?.LodestoneId is null)
                return StatusCode(StatusCodes.Status412PreconditionFailed,
                    "Character's Lodestone profile link was lost between start and verify.");

            var fetch = await bioFetcher.FetchBioAsync(player.Lodestone.LodestoneId.Value, ct);
            if (!fetch.Success)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    $"Lodestone is unreachable right now — try again in a moment ({fetch.ErrorReason}).");

            var bio = fetch.Bio ?? string.Empty;
            var matches = bio.Contains(attempt.Code, StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                var now = DateTime.UtcNow;
                player.ClaimedByUserId = user.Id;
                player.ClaimedAt ??= now;
                player.ClaimVerifiedAt = now;
                _context.ClaimAttempts.Remove(attempt);
                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Concurrent verify by the same user — the other caller's delete already won.
                    // Player claim fields are already set by whichever save committed first; just return 200.
                }
                return Ok(new ClaimVerifyResponse(
                    Claimed: true,
                    CharacterName: player.Name,
                    HomeWorldId: player.HomeWorldId));
            }

            attempt.Attempts++;
            if (attempt.Attempts >= 5)
            {
                _context.ClaimAttempts.Remove(attempt);
                await _context.SaveChangesAsync(ct);
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    "Too many failed attempts — start over with a fresh code.");
            }
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Concurrent verify — re-fetch to get the authoritative Attempts count.
                var reloaded = await _context.ClaimAttempts.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.UserId == user.Id && a.PlayerLocalContentId == localContentId, ct);
                if (reloaded is null)
                    return StatusCode(StatusCodes.Status429TooManyRequests,
                        "Too many failed attempts — start over with a fresh code.");
                return BadRequest(new ClaimVerifyResponse(
                    Claimed: false,
                    AttemptsLeft: Math.Max(0, 5 - reloaded.Attempts)));
            }
            return BadRequest(new ClaimVerifyResponse(
                Claimed: false,
                AttemptsLeft: 5 - attempt.Attempts));
        }

        [HttpDelete("{localContentId:long}/claim")]
        public async Task<IActionResult> Unclaim(long localContentId, CancellationToken ct)
        {
            if (HttpContext.Items["User"] is not ApplicationUser user)
                return Unauthorized();

            var player = await _context.Players.FirstOrDefaultAsync(p => p.LocalContentId == localContentId, ct);
            if (player is null)
                return NotFound();

            if (player.ClaimedByUserId != user.Id)
                return StatusCode(StatusCodes.Status403Forbidden, "You do not own this claim.");

            player.ClaimedByUserId = null;
            player.ClaimedAt = null;
            player.ClaimVerifiedAt = null;
            await _context.SaveChangesAsync(ct);
            return NoContent();
        }

    }
}