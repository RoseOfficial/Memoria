// ASP.NET Core dependencies
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// System dependencies
using System.Text.Json;

// MemoriaServer internal dependencies
using MemoriaServer.Data;
using MemoriaServer.Models.DTOs;
using MemoriaServer.Models.Entities;
using MemoriaServer.Services.Lodestone;
using MemoriaServer.Services.World;

namespace MemoriaServer.Controllers
{
    /// <summary>
    /// API controller for managing player data in the Memoria system.
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
        private readonly MemoriaDbContext _context;
        
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
        public PlayersController(MemoriaDbContext context, ILogger<PlayersController> logger)
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

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 50)
        {
            if (string.IsNullOrWhiteSpace(q)) return Ok(new PlayerSearchResultResponse(Array.Empty<PlayerSearchItem>()));

            var needle = q.Trim();
            if (needle.Length == 0) return Ok(new PlayerSearchResultResponse(Array.Empty<PlayerSearchItem>()));

            // Cap the response. Typeahead callers ask for 10; the /search page asks for 50.
            // Anything above 50 is wasted bandwidth — the UI can't usefully render hundreds.
            var resultLimit = Math.Clamp(limit, 1, 50);

            // Pull a generous set of case-insensitive substring matches, then score in
            // memory so prefix matches outrank embedded substrings. The candidate window is
            // wider than the result limit so the score-then-trim doesn't cut off good
            // matches that happened to be alphabetically late.
            //
            // Lower-casing both sides translates to SQL `LOWER(Name) LIKE %needle%` on
            // Postgres and works on the InMemory provider too — the more idiomatic
            // EF.Functions.ILike is Npgsql-only and throws under InMemory.
            var needleLower = needle.ToLowerInvariant();
            var candidates = await _context.Players
                .Where(p => !p.HideEntirely && !p.IsPrivate && !p.HideInSearch)
                .Where(p => p.Name.ToLower().Contains(needleLower))
                .Take(200)
                .Select(p => new { p.LocalContentId, p.Name, p.HomeWorldId, p.AvatarLink })
                .ToListAsync();

            var ordered = candidates
                .Select(p => new
                {
                    p.LocalContentId,
                    p.Name,
                    p.HomeWorldId,
                    p.AvatarLink,
                    Score = ScoreNameMatch(p.Name, needle),
                })
                .OrderBy(x => x.Score)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Take(resultLimit)
                .ToList();

            var items = ordered.Select(p => new PlayerSearchItem(
                p.LocalContentId,
                p.Name,
                p.HomeWorldId.HasValue ? WorldNames.ToSlug(WorldNames.Resolve(p.HomeWorldId) ?? "unknown") : "unknown",
                p.HomeWorldId.HasValue ? WorldNames.Resolve(p.HomeWorldId) ?? "Unknown" : "Unknown",
                p.AvatarLink)).ToList();

            return Ok(new PlayerSearchResultResponse(items));
        }

        // Lower score = better match. The scale is intentionally coarse so OrderBy is
        // stable and predictable: prefix > word-prefix > substring. Equal scores fall
        // through to alphabetical at the call site. Public so tests can pin the bucket
        // assignment table without spinning up the full controller.
        public static int ScoreNameMatch(string name, string needle)
        {
            if (name.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
                return 0;

            // FFXIV character names are always two space-separated words, so the only
            // word boundary worth checking is " <needle>" anywhere in the string.
            if (name.IndexOf(" " + needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;

            return 2;
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

            // Slug-resolution candidate fetch — project to (LocalContentId, Name) only so we
            // don't drag the multi-KB Lodestone JSON columns over the wire for every player on
            // the world. Once the slug match is found, load the full row by primary key. On a
            // popular DC this drops the candidate query from ~4 KB/row to ~30 bytes/row.
            var slugCandidates = await _context.Players
                .Where(p => p.HomeWorldId == worldId)
                .Select(p => new { p.LocalContentId, p.Name })
                .ToListAsync();
            var matchedId = slugCandidates
                .FirstOrDefault(c => WorldNames.ToSlug(c.Name) == nameLower)?.LocalContentId;

            Player? player = matchedId is null
                ? null
                : await _context.Players.FirstOrDefaultAsync(p => p.LocalContentId == matchedId.Value);

            if (player is null)
            {
                // History fallback: search NameHistory and WorldHistory for any match.
                // NameHistory entries are keyed to a Player whose CURRENT name may differ.
                // Same projection trick — pull only the slug-matchable fields plus the Player
                // FK so we can redirect by id, never the full Player entity.
                var nameHistorySlugMatch = await _context.Set<PlayerNameHistory>()
                    .Where(h => h.Player != null && h.Player.HomeWorldId == worldId)
                    .Select(h => new { h.Name, PlayerId = h.Player!.LocalContentId, PlayerName = h.Player.Name, PlayerHomeWorldId = h.Player.HomeWorldId })
                    .ToListAsync();
                var historicByName = nameHistorySlugMatch
                    .FirstOrDefault(h => WorldNames.ToSlug(h.Name) == nameLower);

                if (historicByName != null)
                    return RedirectToCanonical(historicByName.PlayerName, historicByName.PlayerHomeWorldId);

                var worldHistorySlugMatch = await _context.Set<PlayerWorldHistory>()
                    .Where(h => h.WorldId == worldId && h.Player != null)
                    .Select(h => new { PlayerName = h.Player!.Name, PlayerHomeWorldId = h.Player.HomeWorldId })
                    .ToListAsync();
                var historicByWorld = worldHistorySlugMatch
                    .FirstOrDefault(h => WorldNames.ToSlug(h.PlayerName) == nameLower);

                if (historicByWorld != null)
                    return RedirectToCanonical(historicByWorld.PlayerName, historicByWorld.PlayerHomeWorldId);

                return NotFound();
            }

            var viewerUserId = HttpContext.Items["ViewerUserId"] as int?;
            var isOwner = viewerUserId.HasValue && player.ClaimedByUserId == viewerUserId.Value;
            var isAdmin = (bool)(HttpContext.Items["IsAdmin"] ?? false);

            if (player.HideEntirely && !isOwner && !isAdmin)
                return NotFound();

            var tier = (int)(HttpContext.Items["Tier"] ?? 1);
            var dto = BuildProfileResponse(player, tier, isOwner, isAdmin);
            return Ok(dto);
        }

        private IActionResult RedirectToCanonical(string playerName, short? homeWorldId)
        {
            var worldSlug = WorldNames.ToSlug(WorldNames.Resolve(homeWorldId) ?? "unknown");
            var nameSlug = WorldNames.ToSlug(playerName);
            return RedirectPermanent($"/p/{worldSlug}/{nameSlug}");
        }

        private PlayerProfileResponse BuildProfileResponse(Player player, int tier, bool isOwner, bool isAdmin)
        {
            var worldName = WorldNames.Resolve(player.HomeWorldId) ?? "Unknown";
            var worldSlug = WorldNames.ToSlug(worldName);

            var (currentMountName, currentMountIconUrl) = ResolvePhase1Collectible(
                player.LodestoneMountsData, player.CurrentMountId, "MountId");
            var (currentMinionName, currentMinionIconUrl) = ResolvePhase1Collectible(
                player.LodestoneMinionsData, player.CurrentMinionId, "MinionId");

            var header = new ProfileHeader(
                LocalContentId: player.LocalContentId,
                Name: player.Name,
                WorldSlug: worldSlug,
                WorldName: worldName,
                AvatarUrl: player.AvatarLink,
                PortraitUrl: player.LodestonePortraitUrl,
                CurrentJobId: player.CurrentJobId,
                CurrentJobName: MemoriaServer.Services.Jobs.JobNames.Resolve(player.CurrentJobId),
                CurrentJobLevel: player.CurrentJobLevel,
                FreeCompanyTag: player.FreeCompanyTag,
                LastSeenAt: player.LastScannedAt,
                LastSeenTerritory: null,
                FirstScannedAt: player.CreatedAt,
                OnlineStatusId: player.OnlineStatusId,
                TitleId: player.TitleId,
                GrandCompanyId: player.GrandCompanyId,
                CurrentMountName: currentMountName,
                CurrentMountIconUrl: currentMountIconUrl,
                CurrentMinionName: currentMinionName,
                CurrentMinionIconUrl: currentMinionIconUrl);

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

            // The owner (and admins) always see their own data, even when privacy
            // flags hide it from everyone else — the toggles are about who-else-can-see,
            // not "blind myself to my own profile." Without this bypass the owner's
            // /p/ view shows the same TierGate placeholder a stranger would see and
            // the privacy toggles look like they broke the page.
            var canSeeRestrictedSections = isOwner || isAdmin;

            if (tier >= 2)
            {
                if (!player.HideEncounters || canSeeRestrictedSections)
                {
                    var top = _context.Set<PlayerTerritoryHistory>()
                        .Where(t => t.PlayerLocalContentId == player.LocalContentId)
                        .GroupBy(t => new { t.TerritoryId })
                        .Select(g => new {
                            TerritoryId = g.Key.TerritoryId ?? (short)0,
                            Count = g.Count(),
                            Last = g.Max(x => x.LastSeenAt),
                        })
                        .OrderByDescending(x => x.Count)
                        .Take(5)
                        .ToList();
                    var ids = top.Select(x => x.TerritoryId).ToList();
                    var names = _context.TerritoryNames
                        .Where(tn => ids.Contains(tn.TerritoryId))
                        .ToDictionary(tn => tn.TerritoryId, tn => tn.Name);
                    locations = new LocationsData(top.Select(x =>
                        new TerritoryEntry(
                            x.TerritoryId,
                            names.TryGetValue(x.TerritoryId, out var resolved) ? resolved : $"Territory {x.TerritoryId}",
                            x.Count,
                            x.Last)).ToList());
                }

                var nhRaw = _context.Set<PlayerNameHistory>()
                    .Where(h => h.PlayerLocalContentId == player.LocalContentId)
                    .OrderByDescending(h => h.CreatedAt)
                    .Select(h => new { h.Name, h.CreatedAt })
                    .ToList();
                nameHistory = nhRaw.Select(h => new NameHistoryEntry(h.Name, h.CreatedAt)).ToList();

                var whRaw = _context.Set<PlayerWorldHistory>()
                    .Where(h => h.PlayerLocalContentId == player.LocalContentId)
                    .OrderByDescending(h => h.CreatedAt)
                    .Select(h => new { h.WorldId, h.CreatedAt })
                    .ToList();
                worldHistory = whRaw.Select(h => new WorldHistoryEntry(
                    WorldNames.ToSlug(WorldNames.Resolve(h.WorldId) ?? "unknown"),
                    WorldNames.Resolve(h.WorldId) ?? "Unknown",
                    h.CreatedAt)).ToList();

                if ((!player.HideAlts || canSeeRestrictedSections) && player.AccountId != null)
                {
                    var altsRaw = _context.Players
                        .Where(p => p.AccountId == player.AccountId && p.LocalContentId != player.LocalContentId && !p.HideEntirely)
                        .Select(p => new { p.Name, p.HomeWorldId, p.LocalContentId })
                        .ToList();
                    alts = altsRaw.Select(p => new AltCharacter(
                        p.Name,
                        p.HomeWorldId.HasValue ? WorldNames.ToSlug(WorldNames.Resolve(p.HomeWorldId) ?? "unknown") : "unknown",
                        p.HomeWorldId.HasValue ? WorldNames.Resolve(p.HomeWorldId) ?? "Unknown" : "Unknown",
                        p.LocalContentId)).ToList();
                }
            }

            return new PlayerProfileResponse(header, jobs, customization, mounts, minions,
                locations, nameHistory, worldHistory, alts, isOwner);
        }

        // Look up the currently-summoned mount/minion in the player's own
        // Lodestone collection JSON. Returns (Name, IconUrl) when the id matches
        // an entry, or (null, null) when the id isn't owned (e.g., quest mounts,
        // event minions, or characters whose Lodestone enrichment hasn't run yet).
        // The JSON shape is array of objects with keys "Name", "IconUrl", and
        // either "MountId" or "MinionId" — see CLAUDE.md "Wire format for the
        // JSON columns" section.
        private static (string? Name, string? IconUrl) ResolvePhase1Collectible(
            string? lodestoneJson, int? collectibleId, string idKey)
        {
            if (string.IsNullOrEmpty(lodestoneJson) || !collectibleId.HasValue)
                return (null, null);

            try
            {
                using var doc = JsonDocument.Parse(lodestoneJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return (null, null);

                foreach (var entry in doc.RootElement.EnumerateArray())
                {
                    if (!entry.TryGetProperty(idKey, out var idElement)) continue;
                    if (idElement.ValueKind == JsonValueKind.Null) continue;
                    if (!idElement.TryGetInt32(out var entryId)) continue;
                    if (entryId != collectibleId.Value) continue;

                    string? name = entry.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String
                        ? n.GetString() : null;
                    string? icon = entry.TryGetProperty("IconUrl", out var i) && i.ValueKind == JsonValueKind.String
                        ? i.GetString() : null;
                    return (name, icon);
                }
            }
            catch (JsonException)
            {
                // Malformed JSON — silently fall through to null. Logging would
                // spam the logs since the same row would re-fail every request.
            }
            return (null, null);
        }

        private static bool HasCustomizationChanged(PlayerCustomizationHistory latest, PlayerCustomization current)
        {
            return latest.BodyType != current.BodyType
                || latest.GenderRace != current.GenderRace
                || latest.Height != current.Height
                || latest.Face != current.Face
                || latest.SkinColor != current.SkinColor
                || latest.Nose != current.Nose
                || latest.Jaw != current.Jaw
                || latest.MuscleMass != current.MuscleMass
                || latest.BustSize != current.BustSize
                || latest.TailShape != current.TailShape
                || latest.Mouth != current.Mouth
                || latest.EyeShape != current.EyeShape
                || latest.SmallIris != current.SmallIris;
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
                // Wire format from both the plugin and the server-side enrichment service is an
                // array of objects ([{Name,IconUrl,...}, ...]). We only need Name; icon URLs are
                // reconstructed from the FFXIVCollect slug below so the preview keeps working
                // even if NetStone returned a relative-path Lodestone icon we can't render.
                var entries = JsonSerializer.Deserialize<List<CollectibleEntry>>(json);
                if (entries is null) return null;
                var names = entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                    .Select(e => e.Name!)
                    .ToList();
                if (names.Count == 0) return null;
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

        private sealed record CollectibleEntry(string? Name);

        // Mapping of starter-class ClassJob id → the job(s) it upgrades into. If any
        // upgrade is present in the player's data we suppress the class entry, since
        // Lodestone reports both at the same level and showing "Gladiator 82" next to
        // "Paladin 82" doubles every line in the Jobs panel.
        private static readonly Dictionary<byte, byte[]> ClassToJobUpgrades = new()
        {
            { 1, new byte[] { 19 } },     // Gladiator → Paladin
            { 2, new byte[] { 20 } },     // Pugilist → Monk
            { 3, new byte[] { 21 } },     // Marauder → Warrior
            { 4, new byte[] { 22 } },     // Lancer → Dragoon
            { 5, new byte[] { 23 } },     // Archer → Bard
            { 6, new byte[] { 24 } },     // Conjurer → White Mage
            { 7, new byte[] { 25 } },     // Thaumaturge → Black Mage
            { 26, new byte[] { 27, 28 } }, // Arcanist → Summoner / Scholar
            { 29, new byte[] { 30 } },    // Rogue → Ninja
        };

        private static JobsData BuildJobs(Player player)
        {
            if (string.IsNullOrWhiteSpace(player.LodestoneJobData))
                return new JobsData(Array.Empty<JobEntry>());

            try
            {
                // The plugin's Lodestone scraper emits the dict with stringified ClassJob
                // ids as keys (e.g. {"1":82,"19":82,...}). Parse → dedupe class/job pairs →
                // resolve names → sort.
                var parsed = JsonSerializer.Deserialize<Dictionary<string, short>>(player.LodestoneJobData);
                if (parsed is null) return new JobsData(Array.Empty<JobEntry>());

                var byId = new Dictionary<byte, short>();
                foreach (var kvp in parsed)
                {
                    if (kvp.Value <= 0) continue;
                    if (byte.TryParse(kvp.Key, out var id))
                        byId[id] = kvp.Value;
                }

                // Drop the starter-class entry whenever the upgraded job is also present,
                // regardless of level — covers both "GLA 82, PLD 82" duplicates and the
                // unusual case where the user is currently mid-quest with class > job.
                foreach (var (classId, jobIds) in ClassToJobUpgrades)
                {
                    if (jobIds.Any(j => byId.ContainsKey(j)))
                        byId.Remove(classId);
                }

                return new JobsData(byId
                    .OrderByDescending(kvp => kvp.Value)
                    .Select(kvp => new JobEntry(
                        Name: MemoriaServer.Services.Jobs.JobNames.Resolve(kvp.Key) ?? $"Job {kvp.Key}",
                        Level: kvp.Value))
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

                // Bulk-fetch lightweight slices of existing rows in a single query, projected to
                // skip the multi-KB Lodestone JSON columns. Loading the full Player entity per
                // upload was the dominant Neon egress source: a 50-player batch dragged ~200 KB
                // of JSON over the wire per upload just to evaluate change-detection ifs that
                // could be made on lighter fields. Anonymous-type projection drops that to ~5 KB
                // per batch; the JSON columns are written via stub-entity + selective IsModified.
                var contentIds = players.Select(p => (long)p.LocalContentId).ToList();
                var existingMap = (await _context.Players
                    .Where(p => contentIds.Contains(p.LocalContentId))
                    .Select(p => new
                    {
                        p.LocalContentId,
                        p.Name,
                        p.AccountId,
                        p.HomeWorldId,
                        p.CurrentWorldId,
                        p.TerritoryId,
                        p.PlayerPos,
                        p.CurrentJobId,
                        p.CurrentJobLevel,
                        p.MainJobId,
                        p.MainJobLevel,
                        p.LastJobDataUpdate,
                        p.LastMinionsDataUpdate,
                        p.LastMountsDataUpdate,
                        p.OnlineStatusId,
                        p.TitleId,
                        p.GrandCompanyId,
                        p.FreeCompanyTag,
                        p.CurrentMountId,
                        p.CurrentMinionId,
                    })
                    .ToListAsync())
                    .ToDictionary(p => p.LocalContentId);

                foreach (var playerRequest in players)
                {
                    var lcId = (long)playerRequest.LocalContentId;
                    var requestCreatedAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).UtcDateTime;

                    if (!existingMap.TryGetValue(lcId, out var existing))
                    {
                        // Create new player — full entity insert keeps every column we just received.
                        var newPlayer = new Player
                        {
                            LocalContentId = lcId,
                            Name = playerRequest.Name,
                            AccountId = playerRequest.AccountId,
                            HomeWorldId = (short?)playerRequest.HomeWorldId,
                            CurrentWorldId = (short?)playerRequest.CurrentWorldId,
                            TerritoryId = playerRequest.TerritoryId,
                            CurrentJobId = playerRequest.CurrentJobId,
                            CurrentJobLevel = playerRequest.CurrentJobLevel,
                            PlayerPos = playerRequest.PlayerPos,
                            CreatedAt = requestCreatedAt,
                            LastScannedAt = DateTime.UtcNow,
                            LodestoneJobData = playerRequest.LodestoneJobData,
                            MainJobId = playerRequest.MainJobId,
                            MainJobLevel = playerRequest.MainJobLevel,
                            LastJobDataUpdate = playerRequest.LastJobDataUpdate,
                            LodestoneMinionsData = playerRequest.LodestoneMinionsData,
                            LastMinionsDataUpdate = playerRequest.LastMinionsDataUpdate,
                            LodestoneMountsData = playerRequest.LodestoneMountsData,
                            LastMountsDataUpdate = playerRequest.LastMountsDataUpdate,
                            // Phase 1 — latest-snapshot scalars
                            OnlineStatusId = playerRequest.OnlineStatusId,
                            TitleId = playerRequest.TitleId,
                            GrandCompanyId = playerRequest.GrandCompanyId,
                            FreeCompanyTag = playerRequest.FreeCompanyTag,
                            CurrentMountId = playerRequest.CurrentMountId,
                            CurrentMinionId = playerRequest.CurrentMinionId,
                        };

                        _context.Players.Add(newPlayer);
                    }
                    else
                    {
                        // Update existing player via stub-entity + selective IsModified. The stub
                        // carries only the primary key; properties default to null/zero. Setting
                        // IsModified per touched property tells EF to emit a partial UPDATE that
                        // touches only the columns we changed — JSON columns stay un-read and
                        // un-written unless the request supplies new data for them.
                        var stub = new Player { LocalContentId = lcId };
                        _context.Players.Attach(stub);
                        var entry = _context.Entry(stub);
                        var hasChanges = false;

                        if (existing.Name != playerRequest.Name)
                        {
                            stub.Name = playerRequest.Name;
                            entry.Property(p => p.Name).IsModified = true;
                            hasChanges = true;

                            // Add name history entry
                            _context.PlayerNameHistory.Add(new PlayerNameHistory
                            {
                                PlayerLocalContentId = lcId,
                                Name = playerRequest.Name,
                                CreatedAt = requestCreatedAt,
                            });
                        }

                        // Promote AccountId from null to non-null, but never overwrite a
                        // previously-set value. The earlier "refresh on every update"
                        // amplified leaked bc->AccountId reads from the object-table path
                        // into thousands of fake alt links; insert-only was the immediate
                        // revert. But that left rows whose AccountId got nuked (or never
                        // captured on first sight because the spawn packet fired but the
                        // initial insert had a null value) permanently stuck at null and
                        // unable to surface alts. Spawn-packet captures via GameHooks land
                        // verified per-player AccountIds; allowing them to fill in nulls
                        // (and only nulls) recovers those rows without reintroducing the
                        // overwrite path that caused the corruption.
                        if (existing.AccountId is null && playerRequest.AccountId.HasValue)
                        {
                            stub.AccountId = playerRequest.AccountId;
                            entry.Property(p => p.AccountId).IsModified = true;
                            hasChanges = true;
                        }

                        if (existing.CurrentWorldId != (short?)playerRequest.CurrentWorldId)
                        {
                            stub.CurrentWorldId = (short?)playerRequest.CurrentWorldId;
                            entry.Property(p => p.CurrentWorldId).IsModified = true;
                            hasChanges = true;

                            if (playerRequest.CurrentWorldId.HasValue)
                            {
                                _context.PlayerWorldHistory.Add(new PlayerWorldHistory
                                {
                                    PlayerLocalContentId = lcId,
                                    WorldId = (short)playerRequest.CurrentWorldId.Value,
                                    CreatedAt = requestCreatedAt,
                                });
                            }
                        }

                        // HomeWorldId latest-non-null wins. Was previously set on insert
                        // and never updated, which locked in any wrong-initial-read forever
                        // (e.g. a transient bc->HomeWorld read during DC travel that landed
                        // on the visited world instead of the actual home). Rare paid server
                        // transfers also need this update path. We don't write a history row
                        // here — PlayerWorldHistory is for "worlds seen on" via CurrentWorldId.
                        if (playerRequest.HomeWorldId.HasValue && existing.HomeWorldId != (short?)playerRequest.HomeWorldId)
                        {
                            stub.HomeWorldId = (short?)playerRequest.HomeWorldId;
                            entry.Property(p => p.HomeWorldId).IsModified = true;
                            hasChanges = true;
                        }

                        if (existing.CurrentJobId != playerRequest.CurrentJobId ||
                            existing.CurrentJobLevel != playerRequest.CurrentJobLevel)
                        {
                            stub.CurrentJobId = playerRequest.CurrentJobId;
                            stub.CurrentJobLevel = playerRequest.CurrentJobLevel;
                            entry.Property(p => p.CurrentJobId).IsModified = true;
                            entry.Property(p => p.CurrentJobLevel).IsModified = true;
                            hasChanges = true;
                        }

                        if (existing.TerritoryId != playerRequest.TerritoryId ||
                            existing.PlayerPos != playerRequest.PlayerPos)
                        {
                            stub.TerritoryId = playerRequest.TerritoryId;
                            stub.PlayerPos = playerRequest.PlayerPos;
                            entry.Property(p => p.TerritoryId).IsModified = true;
                            entry.Property(p => p.PlayerPos).IsModified = true;
                            hasChanges = true;

                            // Update or create territory history
                            // Explicit (short?) cast is required: t.WorldId is short (smallint) but
                            // CurrentWorldId is ushort?, and Npgsql refuses to bind a UInt16
                            // parameter as smallint.
                            var territoryHistory = await _context.PlayerTerritoryHistory
                                .FirstOrDefaultAsync(t => t.PlayerLocalContentId == lcId &&
                                                        t.TerritoryId == playerRequest.TerritoryId &&
                                                        t.WorldId == (short?)playerRequest.CurrentWorldId);

                            if (territoryHistory == null && playerRequest.CurrentWorldId.HasValue)
                            {
                                // Only record territory history if we actually have a world id;
                                // otherwise the (short) cast on CurrentWorldId!.Value would NRE.
                                _context.PlayerTerritoryHistory.Add(new PlayerTerritoryHistory
                                {
                                    PlayerLocalContentId = lcId,
                                    TerritoryId = playerRequest.TerritoryId,
                                    PlayerPos = playerRequest.PlayerPos,
                                    WorldId = (short)playerRequest.CurrentWorldId.Value,
                                    FirstSeenAt = requestCreatedAt,
                                    LastSeenAt = requestCreatedAt,
                                });
                            }
                            else if (territoryHistory != null)
                            {
                                territoryHistory.LastSeenAt = requestCreatedAt;
                                territoryHistory.PlayerPos = playerRequest.PlayerPos;
                            }
                        }

                        // Lodestone job data: only update when the request actually carries data.
                        // The previous logic compared LodestoneJobData strings — which forced the
                        // ~4 KB JSON column to be loaded every upload — and would write the
                        // request value verbatim, including null. That meant a plugin upload that
                        // didn't carry Lodestone data would null out server-enriched data on the
                        // row. Guarding on `request != null` preserves the server's data while
                        // letting the plugin's best-effort enrichment ride through when present.
                        if (playerRequest.LodestoneJobData != null)
                        {
                            stub.LodestoneJobData = playerRequest.LodestoneJobData;
                            entry.Property(p => p.LodestoneJobData).IsModified = true;
                            hasChanges = true;
                        }
                        if (playerRequest.MainJobId.HasValue && existing.MainJobId != playerRequest.MainJobId)
                        {
                            stub.MainJobId = playerRequest.MainJobId;
                            entry.Property(p => p.MainJobId).IsModified = true;
                            hasChanges = true;
                        }
                        if (playerRequest.MainJobLevel.HasValue && existing.MainJobLevel != playerRequest.MainJobLevel)
                        {
                            stub.MainJobLevel = playerRequest.MainJobLevel;
                            entry.Property(p => p.MainJobLevel).IsModified = true;
                            hasChanges = true;
                        }
                        if (playerRequest.LastJobDataUpdate.HasValue && existing.LastJobDataUpdate != playerRequest.LastJobDataUpdate)
                        {
                            stub.LastJobDataUpdate = playerRequest.LastJobDataUpdate;
                            entry.Property(p => p.LastJobDataUpdate).IsModified = true;
                            hasChanges = true;
                        }

                        if (playerRequest.LodestoneMinionsData != null)
                        {
                            stub.LodestoneMinionsData = playerRequest.LodestoneMinionsData;
                            entry.Property(p => p.LodestoneMinionsData).IsModified = true;
                            hasChanges = true;
                        }
                        if (playerRequest.LastMinionsDataUpdate.HasValue && existing.LastMinionsDataUpdate != playerRequest.LastMinionsDataUpdate)
                        {
                            stub.LastMinionsDataUpdate = playerRequest.LastMinionsDataUpdate;
                            entry.Property(p => p.LastMinionsDataUpdate).IsModified = true;
                            hasChanges = true;
                        }

                        if (playerRequest.LodestoneMountsData != null)
                        {
                            stub.LodestoneMountsData = playerRequest.LodestoneMountsData;
                            entry.Property(p => p.LodestoneMountsData).IsModified = true;
                            hasChanges = true;
                        }
                        if (playerRequest.LastMountsDataUpdate.HasValue && existing.LastMountsDataUpdate != playerRequest.LastMountsDataUpdate)
                        {
                            stub.LastMountsDataUpdate = playerRequest.LastMountsDataUpdate;
                            entry.Property(p => p.LastMountsDataUpdate).IsModified = true;
                            hasChanges = true;
                        }

                        // Phase 1 — latest non-null wins. Null in the request means
                        // "not observed this scan" (different capture paths see different
                        // subsets), not "explicitly cleared". A field only updates when
                        // the inbound value is non-null.
                        if (playerRequest.OnlineStatusId.HasValue && existing.OnlineStatusId != playerRequest.OnlineStatusId)
                        {
                            stub.OnlineStatusId = playerRequest.OnlineStatusId;
                            entry.Property(p => p.OnlineStatusId).IsModified = true;
                            hasChanges = true;
                        }
                        if (playerRequest.TitleId.HasValue && existing.TitleId != playerRequest.TitleId)
                        {
                            stub.TitleId = playerRequest.TitleId;
                            entry.Property(p => p.TitleId).IsModified = true;
                            hasChanges = true;
                        }
                        if (playerRequest.GrandCompanyId.HasValue && existing.GrandCompanyId != playerRequest.GrandCompanyId)
                        {
                            stub.GrandCompanyId = playerRequest.GrandCompanyId;
                            entry.Property(p => p.GrandCompanyId).IsModified = true;
                            hasChanges = true;
                        }
                        if (!string.IsNullOrEmpty(playerRequest.FreeCompanyTag) && existing.FreeCompanyTag != playerRequest.FreeCompanyTag)
                        {
                            stub.FreeCompanyTag = playerRequest.FreeCompanyTag;
                            entry.Property(p => p.FreeCompanyTag).IsModified = true;
                            hasChanges = true;
                        }
                        if (playerRequest.CurrentMountId.HasValue && existing.CurrentMountId != playerRequest.CurrentMountId)
                        {
                            stub.CurrentMountId = playerRequest.CurrentMountId;
                            entry.Property(p => p.CurrentMountId).IsModified = true;
                            hasChanges = true;
                        }
                        if (playerRequest.CurrentMinionId.HasValue && existing.CurrentMinionId != playerRequest.CurrentMinionId)
                        {
                            stub.CurrentMinionId = playerRequest.CurrentMinionId;
                            entry.Property(p => p.CurrentMinionId).IsModified = true;
                            hasChanges = true;
                        }

                        if (hasChanges)
                        {
                            stub.UpdatedAt = DateTime.UtcNow;
                            entry.Property(p => p.UpdatedAt).IsModified = true;
                        }

                        // Stamp every upload as a scan event regardless of whether other fields
                        // changed — a player who's standing still is still being observed, and the
                        // home page's "recent scans" feed depends on this column being populated.
                        stub.LastScannedAt = DateTime.UtcNow;
                        entry.Property(p => p.LastScannedAt).IsModified = true;
                    }
                }

                await _context.SaveChangesAsync();

                // History backfill — pulls only (LocalContentId, Name, CreatedAt) per player and
                // pre-fetches the existing name-history (player_id, name) pairs in one round-trip.
                // The previous code re-loaded the full Player entity per row (multi-KB JSON
                // columns included) just to read Name + CreatedAt for the backfill, which made
                // this loop another major egress source on every upload.
                var playerMetaMap = (await _context.Players
                    .Where(p => contentIds.Contains(p.LocalContentId))
                    .Select(p => new { p.LocalContentId, p.Name, p.CreatedAt })
                    .ToListAsync())
                    .ToDictionary(p => p.LocalContentId);

                var nameHistoryKeys = await _context.PlayerNameHistory
                    .Where(h => contentIds.Contains(h.PlayerLocalContentId))
                    .Select(h => new { h.PlayerLocalContentId, h.Name })
                    .ToListAsync();
                var existingNameHistory = new HashSet<(long Id, string Name)>(
                    nameHistoryKeys.Select(k => (k.PlayerLocalContentId, k.Name)));

                foreach (var playerRequest in players)
                {
                    var lcId = (long)playerRequest.LocalContentId;
                    if (!playerMetaMap.TryGetValue(lcId, out var meta)) continue;

                    // Check if name history already exists
                    if (!existingNameHistory.Contains((lcId, meta.Name)))
                    {
                        _context.PlayerNameHistory.Add(new PlayerNameHistory
                        {
                            PlayerLocalContentId = lcId,
                            Name = meta.Name,
                            CreatedAt = meta.CreatedAt,
                        });
                        // Mirror the in-memory set so a duplicate within the same batch is filtered.
                        existingNameHistory.Add((lcId, meta.Name));
                    }

                    // Append a customization history row whenever the upload's values
                    // differ from the latest stored row (or when no row exists). The
                    // previous "first scan only" branch meant haircuts, fantasias,
                    // and any post-first-scan customization changes were silently
                    // dropped — and a stale all-null row from an early upload (e.g.,
                    // the JsonPropertyName / JsonProperty mismatch we just fixed)
                    // permanently blocked any real data from being recorded.
                    if (playerRequest.Customization != null)
                    {
                        var latestCustomization = await _context.PlayerCustomizationHistory
                            .Where(h => h.PlayerLocalContentId == lcId)
                            .OrderByDescending(h => h.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (latestCustomization is null
                            || HasCustomizationChanged(latestCustomization, playerRequest.Customization))
                        {
                            var customization = new PlayerCustomizationHistory
                            {
                                PlayerLocalContentId = lcId,
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
                                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).UtcDateTime
                            };
                            _context.PlayerCustomizationHistory.Add(customization);
                        }
                    }
                }

                // Upsert the TerritoryNames lookup for any (territory id, name) pairs the
                // batch carries. The plugin resolves names from Lumina at scan time so
                // this is the only path the server learns zone names — once seen, the
                // Locations panel can show "Limsa Lominsa Upper Decks" instead of
                // "Territory 129". DistinctBy on TerritoryId because every player in the
                // batch was scanned in the same zone, so values repeat.
                var territoryUpserts = players
                    .Where(p => p.TerritoryId.HasValue && !string.IsNullOrWhiteSpace(p.TerritoryName))
                    .Select(p => (Id: p.TerritoryId!.Value, Name: p.TerritoryName!.Trim()))
                    .DistinctBy(t => t.Id)
                    .ToList();
                if (territoryUpserts.Count > 0)
                {
                    var territoryIds = territoryUpserts.Select(t => t.Id).ToList();
                    var existingNames = await _context.TerritoryNames
                        .Where(tn => territoryIds.Contains(tn.TerritoryId))
                        .ToListAsync();
                    var nameByContentId = existingNames.ToDictionary(tn => tn.TerritoryId);
                    var now = DateTime.UtcNow;
                    foreach (var (id, name) in territoryUpserts)
                    {
                        if (nameByContentId.TryGetValue(id, out var row))
                        {
                            // Only stamp on actual changes — most uploads see existing rows
                            // and rewriting LastUpdatedAt every batch would wear the index.
                            if (row.Name != name)
                            {
                                row.Name = name;
                                row.LastUpdatedAt = now;
                            }
                        }
                        else
                        {
                            _context.TerritoryNames.Add(new TerritoryName
                            {
                                TerritoryId = id,
                                Name = name,
                                LastUpdatedAt = now,
                            });
                        }
                    }
                }

                // Stamp the authenticated uploader's lifetime contribution counter and
                // per-player scan attribution so the /me dashboard can show both the
                // running total and a recent-contributions list. Lifetime mirrors the
                // plugin-side counter at PersistenceContext.PostPlayerData; per-player
                // attribution is server-only since the plugin doesn't track it.
                if (HttpContext.Items["User"] is ApplicationUser uploader && players.Count > 0)
                {
                    uploader.TotalContributions += players.Count;

                    // contentIds is already declared at the top of UploadPlayers — reuse it.
                    var existingScans = await _context.UserScannedPlayers
                        .Where(s => s.UserId == uploader.Id && contentIds.Contains(s.PlayerLocalContentId))
                        .ToListAsync();
                    var scanByContentId = existingScans.ToDictionary(s => s.PlayerLocalContentId);
                    var scannedAt = DateTime.UtcNow;
                    foreach (var playerRequest in players)
                    {
                        var contentId = (long)playerRequest.LocalContentId;
                        if (scanByContentId.TryGetValue(contentId, out var existingScan))
                        {
                            existingScan.LastScannedAt = scannedAt;
                        }
                        else
                        {
                            _context.UserScannedPlayers.Add(new UserScannedPlayer
                            {
                                UserId = uploader.Id,
                                PlayerLocalContentId = contentId,
                                LastScannedAt = scannedAt,
                            });
                        }
                    }
                }

                // Save history entries
                await _context.SaveChangesAsync();

                // Hand the uploaded ContentIds to the centralized enrichment service. It owns
                // the Lodestone fetch loop (avatar + jobs + minions + mounts) and rate-limits
                // globally, so this controller stops doing per-batch fetches.
                var enrichmentService = HttpContext.RequestServices.GetService<LodestoneEnrichmentService>();
                if (enrichmentService is not null)
                {
                    foreach (var playerRequest in players)
                        enrichmentService.Enqueue((long)playerRequest.LocalContentId);
                }

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
                    Code = MemoriaServer.Services.Auth.ClaimCodeGenerator.GenerateClaimCode(),
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    Attempts = 0,
                };
                _context.ClaimAttempts.Add(existing);
            }
            else
            {
                existing.Code = MemoriaServer.Services.Auth.ClaimCodeGenerator.GenerateClaimCode();
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
            [FromServices] MemoriaServer.Services.Lodestone.ILodestoneBioFetcher bioFetcher,
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