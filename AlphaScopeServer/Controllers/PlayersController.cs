// ASP.NET Core dependencies
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// AlphaScopeServer internal dependencies
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;

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

                // Apply privacy filter (hide private players unless owned by current user)
                var gameAccountId = HttpContext.Items["GameAccountId"] as int?;
                query = query.Where(p => !p.IsPrivate || p.AccountId == gameAccountId);

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
                        CurrentJobLevel = p.CurrentJobLevel
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
                    .Include(p => p.Retainers)
                        .ThenInclude(r => r.NameHistory)
                    .Include(p => p.Retainers)
                        .ThenInclude(r => r.WorldHistory)
                    .Include(p => p.ProfileVisits)
                    .FirstOrDefaultAsync(p => p.LocalContentId == id);

                if (player == null)
                {
                    return NotFound("Player not found");
                }

                // Check privacy settings
                var gameAccountId = HttpContext.Items["GameAccountId"] as int?;
                if (player.IsPrivate && player.AccountId != gameAccountId)
                {
                    return Forbid("Player profile is private");
                }

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
                    Retainers = player.Retainers.Select(r => new RetainerDetailedDto
                    {
                        LocalContentId = r.LocalContentId,
                        OwnerLocalContentId = r.OwnerLocalContentId,
                        LastSeen = (int)new DateTimeOffset(r.LastSeen, TimeSpan.Zero).ToUnixTimeSeconds(),
                        Names = r.NameHistory.Select(n => new RetainerNameHistoryDto
                        {
                            Name = n.Name,
                            CreatedAt = (int)new DateTimeOffset(n.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                        }).ToList(),
                        Worlds = r.WorldHistory.Select(w => new RetainerWorldHistoryDto
                        {
                            WorldId = w.WorldId,
                            CreatedAt = (int)new DateTimeOffset(w.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                        }).ToList()
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

                // Record profile visit
                if (gameAccountId.HasValue)
                {
                    var visit = new PlayerProfileVisit
                    {
                        PlayerLocalContentId = id,
                        VisitorId = gameAccountId.ToString(),
                        VisitedAt = DateTime.UtcNow
                    };
                    _context.PlayerProfileVisits.Add(visit);
                    await _context.SaveChangesAsync();
                }

                return Ok(detailed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player {PlayerId}", id);
                return StatusCode(500, "Error retrieving player details");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadPlayers([FromBody] List<PostPlayerRequest> players)
        {
            try
            {
                var gameAccountId = HttpContext.Items["GameAccountId"] as int?;
                if (!gameAccountId.HasValue)
                {
                    return Unauthorized("Game account ID not found");
                }

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
                            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).DateTime
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
                                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).DateTime
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
                                    CreatedAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).DateTime
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
                            var territoryHistory = await _context.PlayerTerritoryHistory
                                .FirstOrDefaultAsync(t => t.PlayerLocalContentId == existingPlayer.LocalContentId &&
                                                        t.TerritoryId == playerRequest.TerritoryId &&
                                                        t.WorldId == playerRequest.CurrentWorldId);

                            if (territoryHistory == null)
                            {
                                territoryHistory = new PlayerTerritoryHistory
                                {
                                    PlayerLocalContentId = existingPlayer.LocalContentId,
                                    TerritoryId = playerRequest.TerritoryId,
                                    PlayerPos = playerRequest.PlayerPos,
                                    WorldId = (short)playerRequest.CurrentWorldId!.Value,
                                    FirstSeenAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).DateTime,
                                    LastSeenAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).DateTime
                                };
                                _context.PlayerTerritoryHistory.Add(territoryHistory);
                            }
                            else
                            {
                                territoryHistory.LastSeenAt = DateTimeOffset.FromUnixTimeSeconds(playerRequest.CreatedAt).DateTime;
                                territoryHistory.PlayerPos = playerRequest.PlayerPos;
                            }
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

                // Auto-link Lodestone profiles for new/updated players (background task)
                var serviceScope = HttpContext.RequestServices.CreateScope();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = serviceScope;
                        var scopedContext = scope.ServiceProvider.GetRequiredService<AlphaScopeDbContext>();
                        var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<PlayersController>>();
                        
                        foreach (var playerRequest in players)
                        {
                            var player = await scopedContext.Players
                                .FirstOrDefaultAsync(p => p.LocalContentId == (long)playerRequest.LocalContentId);
                            
                            if (player != null)
                            {
                                scopedLogger.LogInformation($"Checking player {player.Name} for auto-linking. Current AvatarLink: '{player.AvatarLink}', IsNullOrEmpty: {string.IsNullOrEmpty(player.AvatarLink)}");
                                
                                if (string.IsNullOrEmpty(player.AvatarLink))
                                {
                                    scopedLogger.LogInformation($"Starting auto-link process for {player.Name}");
                                    await AutoLinkLodestoneForPlayer(player, scopedContext, scopedLogger);
                                    await scopedContext.SaveChangesAsync();
                                    
                                    // Add delay to avoid rate limiting
                                    await Task.Delay(2000);
                                }
                                else
                                {
                                    scopedLogger.LogInformation($"Skipping {player.Name} - already has avatar link");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in background Lodestone auto-linking");
                    }
                });

                // Update user stats
                var user = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == gameAccountId.Value);
                if (user != null)
                {
                    user.UploadedPlayersCount += players.Count;
                    user.UploadedPlayerInfoCount += players.Count;
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Players uploaded successfully", count = players.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading players");
                return StatusCode(500, "Error uploading players");
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

                        // Check for exact match
                        if (string.Equals(foundName, characterName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(foundWorld, worldName, StringComparison.OrdinalIgnoreCase))
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

                // Get world name
                var worldName = GetWorldNameFromId(player.HomeWorldId ?? player.CurrentWorldId);
                if (string.IsNullOrEmpty(worldName))
                {
                    logger.LogWarning($"Could not determine world name for player {player.Name} (WorldId: {player.HomeWorldId ?? player.CurrentWorldId})");
                    return false;
                }

                // Search Lodestone for this character
                var lodestoneId = await SearchLodestoneCharacter(player.Name, worldName, logger);
                if (!lodestoneId.HasValue)
                {
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

        private string? GetWorldNameFromId(short? worldId)
        {
            if (!worldId.HasValue) return null;
            
            // World mapping for FFXIV worlds
            var worldMap = new Dictionary<short, string>
            {
                // Aether Data Center
                { 34, "Brynhildr" }, { 37, "Diabolos" }, { 40, "Malboro" }, { 41, "Mateus" },
                { 53, "Adamantoise" }, { 54, "Cactuar" }, { 57, "Faerie" }, { 58, "Gilgamesh" },
                { 63, "Jenova" }, { 64, "Midgardsormr" }, { 65, "Sargatanas" }, { 67, "Siren" },
                
                // Primal Data Center  
                { 35, "Exodus" }, { 68, "Behemoth" }, { 69, "Excalibur" }, { 71, "Famfrit" },
                { 74, "Hyperion" }, { 78, "Lamia" }, { 79, "Leviathan" }, { 81, "Ultros" },
                
                // Crystal Data Center
                { 95, "Balmung" }, { 99, "Goblin" }, { 100, "Zalera" }, { 76, "Halicarnassus" },
                
                // Chaos Data Center (EU)
                { 80, "Cerberus" }, { 33, "Louise" }, { 36, "Moogle" }, { 56, "Omega" },
                { 66, "Ragnarok" }, { 77, "Spriggan" },
                
                // Light Data Center (EU)
                { 42, "Lich" }, { 39, "Odin" }, { 59, "Phoenix" }, { 83, "Shiva" },
                { 97, "Twintania" }, { 401, "Alpha" }, { 402, "Raiden" },
                
                // Add more as needed...
            };

            return worldMap.TryGetValue(worldId.Value, out var worldName) ? worldName : null;
        }

        [HttpPost("test-auto-link/{playerName}")]
        public async Task<IActionResult> TestAutoLink(string playerName)
        {
            try
            {
                var player = await _context.Players
                    .FirstOrDefaultAsync(p => p.Name == playerName);

                if (player == null)
                {
                    return NotFound($"Player {playerName} not found");
                }

                _logger.LogInformation($"Manual test auto-link for {playerName}. Current AvatarLink: '{player.AvatarLink}'");
                
                // Clear existing avatar link to force re-linking
                player.AvatarLink = null;
                await _context.SaveChangesAsync();

                var result = await AutoLinkLodestoneForPlayer(player, _context, _logger);
                await _context.SaveChangesAsync();

                return Ok(new { 
                    success = result, 
                    playerName = player.Name,
                    avatarLink = player.AvatarLink,
                    worldId = player.HomeWorldId ?? player.CurrentWorldId,
                    worldName = GetWorldNameFromId(player.HomeWorldId ?? player.CurrentWorldId)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in manual auto-link test for {playerName}");
                return StatusCode(500, ex.Message);
            }
        }
    }
}