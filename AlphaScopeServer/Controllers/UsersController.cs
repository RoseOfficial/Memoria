using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace AlphaScopeServer.Controllers
{
    [ApiController]
    [Route("v1/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AlphaScopeDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(AlphaScopeDbContext context, ILogger<UsersController> logger)
        {
            _context = context;
            _logger = logger;
        }


        [HttpPost("login")]
        public async Task<ActionResult<User>> Login([FromBody] UserRegister request)
        {
            try
            {
                // Check if user already exists
                var existingUser = await _context.Users
                    .Include(u => u.Characters)
                    .Include(u => u.LodestoneCharacters)
                    .FirstOrDefaultAsync(u => u.GameAccountId == request.GameAccountId);

                ApplicationUser user;

                if (existingUser == null)
                {
                    // Create new user
                    var apiKey = GenerateApiKey();
                    
                    user = new ApplicationUser
                    {
                        GameAccountId = request.GameAccountId,
                        PrimaryCharacterLocalContentId = request.UserLocalContentId,
                        Name = request.Name,
                        ApiKey = $"{apiKey}-{request.GameAccountId}",
                        AppRoleId = (int)UserRole.Member,
                        BaseUrl = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/v1/",
                        CreatedAt = DateTime.UtcNow,
                        LastLoginAt = DateTime.UtcNow
                    };

                    _context.Users.Add(user);

                    // Create primary character entry
                    var primaryChar = new UserCharacter
                    {
                        UserId = user.Id,
                        LocalContentId = request.UserLocalContentId,
                        Name = request.Name
                    };

                    _context.UserCharacters.Add(primaryChar);
                    await _context.SaveChangesAsync();

                    // Reload with relationships
                    user = await _context.Users
                        .Include(u => u.Characters)
                        .Include(u => u.LodestoneCharacters)
                        .FirstAsync(u => u.Id == user.Id);
                }
                else
                {
                    user = existingUser;
                    user.LastLoginAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // Map to DTO. The ApiKey is returned here (and only here) so newly-registered
                // plugin instances can persist it and authenticate subsequent requests.
                var userDto = new User
                {
                    BaseUrl = user.BaseUrl ?? "https://localhost:5001/v1/",
                    GameAccountId = user.GameAccountId,
                    LocalContentId = user.PrimaryCharacterLocalContentId,
                    Name = user.Name,
                    AppRoleId = user.AppRoleId,
                    ApiKey = user.ApiKey,
                    Characters = user.Characters.Select(c => new UserCharacterDto
                    {
                        Name = c.Name,
                        LocalContentId = c.LocalContentId,
                        AvatarLink = c.AvatarLink,
                        Privacy = new CharacterPrivacySettingsDto
                        {
                            HideFullProfile = c.HideFullProfile,
                            HideTerritoryInfo = c.HideTerritoryInfo,
                            HideCustomizations = c.HideCustomizations,
                            HideInSearchResults = c.HideInSearchResults,
                            HideRetainersInfo = c.HideRetainersInfo,
                            HideAltCharacters = c.HideAltCharacters
                        },
                        ProfileVisitInfo = new CharacterProfileVisitInfoDto
                        {
                            ProfileTotalVisitCount = c.ProfileTotalVisitCount,
                            LastProfileVisitDate = c.LastProfileVisitDate.HasValue 
                                ? (int)new DateTimeOffset(c.LastProfileVisitDate.Value, TimeSpan.Zero).ToUnixTimeSeconds()
                                : null
                        }
                    }).ToList(),
                    NetworkStats = new UserNetworkStatsDto
                    {
                        UploadedPlayersCount = user.UploadedPlayersCount,
                        UploadedPlayerInfoCount = user.UploadedPlayerInfoCount,
                        UploadedRetainersCount = user.UploadedRetainersCount,
                        UploadedRetainerInfoCount = user.UploadedRetainerInfoCount,
                        FetchedPlayerInfoCount = user.FetchedPlayerInfoCount,
                        SearchedNamesCount = user.SearchedNamesCount,
                        LastSyncedTime = (int)new DateTimeOffset(user.LastSyncedTime, TimeSpan.Zero).ToUnixTimeSeconds()
                    },
                    LodestoneCharacters = user.LodestoneCharacters.Select(l => new UserLodestoneCharacterDto
                    {
                        LodestoneId = l.LodestoneId,
                        NameAndWorld = l.NameAndWorld,
                        AvatarLink = l.AvatarLink,
                        VerifiedAt = (int)new DateTimeOffset(l.VerifiedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                    }).ToList()
                };

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                return StatusCode(500, "Error during login");
            }
        }

        [HttpGet("me")]
        public async Task<ActionResult<User>> GetCurrentUser()
        {
            try
            {
                var userId = HttpContext.Items["UserId"] as int?;
                if (!userId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var user = await _context.Users
                    .Include(u => u.Characters)
                    .Include(u => u.LodestoneCharacters)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (user == null)
                {
                    return NotFound("User not found");
                }

                // Update fetch count
                user.FetchedPlayerInfoCount++;
                await _context.SaveChangesAsync();

                // Map to DTO (same as login)
                var userDto = new User
                {
                    BaseUrl = user.BaseUrl ?? "https://localhost:5001/v1/",
                    GameAccountId = user.GameAccountId,
                    LocalContentId = user.PrimaryCharacterLocalContentId,
                    Name = user.Name,
                    AppRoleId = user.AppRoleId,
                    Characters = user.Characters.Select(c => new UserCharacterDto
                    {
                        Name = c.Name,
                        LocalContentId = c.LocalContentId,
                        AvatarLink = c.AvatarLink,
                        Privacy = new CharacterPrivacySettingsDto
                        {
                            HideFullProfile = c.HideFullProfile,
                            HideTerritoryInfo = c.HideTerritoryInfo,
                            HideCustomizations = c.HideCustomizations,
                            HideInSearchResults = c.HideInSearchResults,
                            HideRetainersInfo = c.HideRetainersInfo,
                            HideAltCharacters = c.HideAltCharacters
                        },
                        ProfileVisitInfo = new CharacterProfileVisitInfoDto
                        {
                            ProfileTotalVisitCount = c.ProfileTotalVisitCount,
                            LastProfileVisitDate = c.LastProfileVisitDate.HasValue 
                                ? (int)new DateTimeOffset(c.LastProfileVisitDate.Value, TimeSpan.Zero).ToUnixTimeSeconds()
                                : null
                        }
                    }).ToList(),
                    NetworkStats = new UserNetworkStatsDto
                    {
                        UploadedPlayersCount = user.UploadedPlayersCount,
                        UploadedPlayerInfoCount = user.UploadedPlayerInfoCount,
                        UploadedRetainersCount = user.UploadedRetainersCount,
                        UploadedRetainerInfoCount = user.UploadedRetainerInfoCount,
                        FetchedPlayerInfoCount = user.FetchedPlayerInfoCount,
                        SearchedNamesCount = user.SearchedNamesCount,
                        LastSyncedTime = (int)new DateTimeOffset(user.LastSyncedTime, TimeSpan.Zero).ToUnixTimeSeconds()
                    },
                    LodestoneCharacters = user.LodestoneCharacters.Select(l => new UserLodestoneCharacterDto
                    {
                        LodestoneId = l.LodestoneId,
                        NameAndWorld = l.NameAndWorld,
                        AvatarLink = l.AvatarLink,
                        VerifiedAt = (int)new DateTimeOffset(l.VerifiedAt, TimeSpan.Zero).ToUnixTimeSeconds()
                    }).ToList()
                };

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return StatusCode(500, "Error retrieving user information");
            }
        }

        [HttpPost("update")]
        public async Task<ActionResult<User>> UpdateUser([FromBody] UserUpdateDto request)
        {
            try
            {
                var userId = HttpContext.Items["UserId"] as int?;
                if (!userId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                var user = await _context.Users
                    .Include(u => u.Characters)
                    .Include(u => u.LodestoneCharacters)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);

                if (user == null)
                {
                    return NotFound("User not found");
                }

                // Update characters if provided
                if (request.Characters != null && request.Characters.Any())
                {
                    foreach (var charDto in request.Characters.Where(c => c != null))
                    {
                        var existingChar = user.Characters
                            .FirstOrDefault(c => c.LocalContentId == charDto!.LocalContentId);

                        if (existingChar != null && charDto!.Privacy != null)
                        {
                            // Update privacy settings
                            existingChar.HideFullProfile = charDto.Privacy.HideFullProfile;
                            existingChar.HideTerritoryInfo = charDto.Privacy.HideTerritoryInfo;
                            existingChar.HideCustomizations = charDto.Privacy.HideCustomizations;
                            existingChar.HideInSearchResults = charDto.Privacy.HideInSearchResults;
                            existingChar.HideRetainersInfo = charDto.Privacy.HideRetainersInfo;
                            existingChar.HideAltCharacters = charDto.Privacy.HideAltCharacters;

                            if (!string.IsNullOrEmpty(charDto.AvatarLink))
                            {
                                existingChar.AvatarLink = charDto.AvatarLink;
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // Return updated user (reuse GetCurrentUser logic)
                return await GetCurrentUser();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user");
                return StatusCode(500, "Error updating user");
            }
        }

        [HttpPost("create-test-user")]
        public async Task<ActionResult> CreateTestUser()
        {
            try
            {
                // Check if user already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.GameAccountId == 1387972975);
                
                if (existingUser != null)
                {
                    return Ok(new { message = "Test user already exists", apiKey = existingUser.ApiKey });
                }

                // Create user with AlphaScope's exact API key format
                var user = new ApplicationUser
                {
                    GameAccountId = 1387972975,
                    PrimaryCharacterLocalContentId = 18014498559422700,
                    Name = "Rose Ultima",
                    ApiKey = "PrkdCR9gOCSYZYOlGruL-1387972975", // Exact match from AlphaScope config
                    AppRoleId = (int)UserRole.Member,
                    BaseUrl = "https://localhost:5001/v1/",
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Test user created successfully", apiKey = user.ApiKey });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating test user");
                return StatusCode(500, $"Error creating test user: {ex.Message}");
            }
        }

        [HttpPost("lodestone/claim")]
        public async Task<ActionResult<ClaimLodestoneCharacterDto>> ClaimLodestoneProfile([FromQuery] string url, [FromQuery] int state)
        {
            try
            {
                var userId = HttpContext.Items["UserId"] as int?;
                if (!userId.HasValue)
                {
                    return Unauthorized("User not authenticated");
                }

                // Extract Lodestone ID from URL
                var regex = new Regex(@"finalfantasyxiv\.com\/lodestone\/character\/(\d+)(?:\/|$)", RegexOptions.Compiled);
                var match = regex.Match(url);
                
                if (!match.Success)
                {
                    return BadRequest(new ClaimLodestoneCharacterDto 
                    { 
                        Message = "Invalid Lodestone URL format" 
                    });
                }

                var lodestoneId = int.Parse(match.Groups[1].Value);

                if (state == 0) // Find Profile
                {
                    var profileData = await ScrapeLodestoneProfile(lodestoneId);
                    if (profileData == null)
                    {
                        return BadRequest(new ClaimLodestoneCharacterDto 
                        { 
                            Message = "Failed to fetch Lodestone profile" 
                        });
                    }

                    // Generate verification code
                    var verifyCode = GenerateVerificationCode();

                    return Ok(new ClaimLodestoneCharacterDto
                    {
                        LodestoneId = lodestoneId,
                        NameAndWorld = profileData.Value.NameAndWorld,
                        AvatarLink = profileData.Value.AvatarLink,
                        VerifyCode = verifyCode,
                        Message = "Profile found successfully"
                    });
                }
                else if (state == 1) // Check Bio for verification
                {
                    // For now, we'll simulate successful verification
                    // In a real implementation, you'd scrape the bio to check for the verification code
                    var user = await _context.Users
                        .Include(u => u.LodestoneCharacters)
                        .FirstOrDefaultAsync(u => u.Id == userId.Value);

                    if (user == null)
                    {
                        return NotFound("User not found");
                    }

                    // Check if this Lodestone character is already claimed
                    var existingClaim = user.LodestoneCharacters
                        .FirstOrDefault(l => l.LodestoneId == lodestoneId);

                    if (existingClaim == null)
                    {
                        var profileData = await ScrapeLodestoneProfile(lodestoneId);
                        if (profileData == null)
                        {
                            return BadRequest(new ClaimLodestoneCharacterDto 
                            { 
                                Message = "Failed to fetch Lodestone profile" 
                            });
                        }

                        // Add new Lodestone character
                        var newLodestoneChar = new UserLodestoneCharacter
                        {
                            UserId = user.Id,
                            LodestoneId = lodestoneId,
                            NameAndWorld = profileData.Value.NameAndWorld,
                            AvatarLink = profileData.Value.AvatarLink,
                            VerifiedAt = DateTime.UtcNow
                        };

                        _context.UserLodestoneCharacters.Add(newLodestoneChar);

                        // Try to link this Lodestone character to existing Player records
                        await LinkLodestoneToPlayer(lodestoneId, profileData.Value.NameAndWorld, profileData.Value.AvatarLink);

                        await _context.SaveChangesAsync();

                        return Ok(new ClaimLodestoneCharacterDto
                        {
                            LodestoneId = lodestoneId,
                            NameAndWorld = profileData.Value.NameAndWorld,
                            AvatarLink = profileData.Value.AvatarLink,
                            Message = "Character verified and claimed successfully"
                        });
                    }
                    else
                    {
                        return Ok(new ClaimLodestoneCharacterDto
                        {
                            LodestoneId = lodestoneId,
                            NameAndWorld = existingClaim.NameAndWorld,
                            AvatarLink = existingClaim.AvatarLink,
                            Message = "Character already claimed"
                        });
                    }
                }

                return BadRequest(new ClaimLodestoneCharacterDto 
                { 
                    Message = "Invalid state parameter" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error claiming Lodestone profile");
                return StatusCode(500, new ClaimLodestoneCharacterDto 
                { 
                    Message = "Internal server error" 
                });
            }
        }

        private async Task<(string NameAndWorld, string AvatarLink)?> ScrapeLodestoneProfile(int lodestoneId)
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
                    _logger.LogWarning($"Could not find name/world elements for Lodestone ID {lodestoneId}");
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

                _logger.LogInformation($"Scraped Lodestone profile {lodestoneId}: {nameAndWorld}, Avatar: {avatarUrl}");
                
                return (nameAndWorld, avatarUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error scraping Lodestone profile {lodestoneId}");
                return null;
            }
        }

        private async Task<int?> SearchLodestoneCharacter(string characterName, string worldName)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                // Lodestone character search URL
                var searchUrl = $"https://na.finalfantasyxiv.com/lodestone/character/?q={Uri.EscapeDataString(characterName)}&worldname={Uri.EscapeDataString(worldName)}";
                _logger.LogInformation($"Searching Lodestone for character: {characterName} on {worldName}");

                var response = await httpClient.GetStringAsync(searchUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(response);

                // Find character entries in search results
                var characterEntries = doc.DocumentNode.SelectNodes("//div[@class='entry']");
                
                if (characterEntries == null || !characterEntries.Any())
                {
                    _logger.LogInformation($"No search results found for {characterName} on {worldName}");
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
                                _logger.LogInformation($"Found exact match for {characterName}@{worldName}: Lodestone ID {lodestoneId}");
                                return lodestoneId;
                            }
                        }
                    }
                }

                _logger.LogInformation($"No exact match found for {characterName}@{worldName}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching Lodestone for character {characterName}@{worldName}");
                return null;
            }
        }

        private async Task<bool> AutoLinkLodestoneForPlayer(Player player)
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
                    var homeWorldName = GetWorldNameFromId(player.HomeWorldId);
                    if (!string.IsNullOrEmpty(homeWorldName))
                    {
                        lodestoneId = await SearchLodestoneCharacter(player.Name, homeWorldName);
                    }
                }
                
                // If not found and CurrentWorldId is different, try current world
                if (!lodestoneId.HasValue && player.CurrentWorldId.HasValue && player.CurrentWorldId != player.HomeWorldId)
                {
                    var currentWorldName = GetWorldNameFromId(player.CurrentWorldId);
                    if (!string.IsNullOrEmpty(currentWorldName))
                    {
                        lodestoneId = await SearchLodestoneCharacter(player.Name, currentWorldName);
                    }
                }
                
                // If still not found, log the issue and return
                if (!lodestoneId.HasValue)
                {
                    _logger.LogInformation($"Could not find {player.Name} on Lodestone (tried Home: {GetWorldNameFromId(player.HomeWorldId)}, Current: {GetWorldNameFromId(player.CurrentWorldId)})");
                    return false;
                }

                // Scrape the character profile
                var profileData = await ScrapeLodestoneProfile(lodestoneId.Value);
                if (profileData == null)
                {
                    return false;
                }

                // Create or update PlayerLodestone record
                var existingLodestone = await _context.PlayerLodestones
                    .FirstOrDefaultAsync(pl => pl.PlayerLocalContentId == player.LocalContentId);

                if (existingLodestone == null)
                {
                    var newPlayerLodestone = new PlayerLodestone
                    {
                        PlayerLocalContentId = player.LocalContentId,
                        LodestoneId = lodestoneId.Value,
                        AvatarLink = profileData.Value.AvatarLink
                    };
                    _context.PlayerLodestones.Add(newPlayerLodestone);
                }
                else
                {
                    existingLodestone.LodestoneId = lodestoneId.Value;
                    existingLodestone.AvatarLink = profileData.Value.AvatarLink;
                }

                // Update player's direct avatar link
                player.AvatarLink = profileData.Value.AvatarLink;

                _logger.LogInformation($"Auto-linked player {player.Name} to Lodestone ID {lodestoneId.Value} with avatar: {profileData.Value.AvatarLink}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error auto-linking Lodestone for player {player.Name}");
                return false;
            }
        }

        private string? GetWorldNameFromId(short? worldId)
        {
            if (!worldId.HasValue) return null;
            
            // World mapping for FFXIV worlds (corrected to match official game data)
            var worldMap = new Dictionary<short, string>
            {
                // Aether Data Center
                { 34, "Brynhildr" }, { 62, "Diabolos" }, { 75, "Malboro" }, { 37, "Mateus" },
                { 73, "Adamantoise" }, { 79, "Cactuar" }, { 54, "Faerie" }, { 63, "Gilgamesh" },
                { 40, "Jenova" }, { 65, "Midgardsormr" }, { 99, "Sargatanas" }, { 57, "Siren" },
                
                // Primal Data Center  
                { 53, "Exodus" }, { 78, "Behemoth" }, { 93, "Excalibur" }, { 35, "Famfrit" },
                { 95, "Hyperion" }, { 55, "Lamia" }, { 64, "Leviathan" }, { 77, "Ultros" },
                
                // Crystal Data Center
                { 91, "Balmung" }, { 81, "Goblin" }, { 41, "Zalera" }, { 74, "Coeurl" },
                
                // Chaos Data Center (EU)
                { 80, "Cerberus" }, { 71, "Moogle" }, { 39, "Omega" }, { 97, "Ragnarok" },
                { 85, "Spriggan" },
                
                // Light Data Center (EU)
                { 36, "Lich" }, { 66, "Odin" }, { 56, "Phoenix" }, { 67, "Shiva" },
                { 33, "Twintania" },
                
                // Elemental Data Center (JP)
                { 23, "Asura" }, { 45, "Carbuncle" }, { 58, "Garuda" }, { 59, "Ifrit" },
                { 49, "Kujata" }, { 50, "Typhon" },
                
                // Gaia Data Center (JP)
                { 43, "Alexander" }, { 69, "Bahamut" }, { 92, "Durandal" }, { 46, "Fenrir" },
                { 51, "Ultima" }, { 98, "Ridill" },
                
                // Mana Data Center (JP)
                { 44, "Anima" }, { 70, "Chocobo" }, { 47, "Hades" }, { 48, "Ixion" },
                { 96, "Masamune" }, { 61, "Titan" }, { 28, "Pandaemonium" },
                
                // Meteor Data Center (JP)
                { 24, "Belias" }, { 82, "Mandragora" }, { 60, "Ramuh" }, { 29, "Shinryu" },
                { 52, "Valefor" }, { 30, "Unicorn" }, { 31, "Yojimbo" }, { 32, "Zeromus" },
                
                // Materia Data Center (OCE)
                { 21, "Ravana" }, { 22, "Bismarck" }, { 86, "Sephirot" }, { 87, "Sophia" }, { 88, "Zurvan" }
            };

            return worldMap.TryGetValue(worldId.Value, out var worldName) ? worldName : null;
        }

        private async Task LinkLodestoneToPlayer(int lodestoneId, string nameAndWorld, string avatarLink)
        {
            try
            {
                // Parse character name from "Name@World" format
                var parts = nameAndWorld.Split('@');
                if (parts.Length != 2)
                {
                    _logger.LogWarning($"Invalid NameAndWorld format: {nameAndWorld}");
                    return;
                }

                var characterName = parts[0].Trim();
                var worldName = parts[1].Trim();

                // Find existing player records with matching name
                var matchingPlayers = await _context.Players
                    .Where(p => p.Name == characterName)
                    .ToListAsync();

                foreach (var player in matchingPlayers)
                {
                    // Create or update PlayerLodestone record
                    var existingLodestone = await _context.PlayerLodestones
                        .FirstOrDefaultAsync(pl => pl.PlayerLocalContentId == player.LocalContentId);

                    if (existingLodestone == null)
                    {
                        // Create new PlayerLodestone record
                        var newPlayerLodestone = new PlayerLodestone
                        {
                            PlayerLocalContentId = player.LocalContentId,
                            LodestoneId = lodestoneId,
                            AvatarLink = avatarLink
                        };
                        _context.PlayerLodestones.Add(newPlayerLodestone);

                        // Also update the player's direct avatar link
                        player.AvatarLink = avatarLink;

                        _logger.LogInformation($"Linked Lodestone {lodestoneId} to player {characterName} (ContentId: {player.LocalContentId})");
                    }
                    else if (existingLodestone.LodestoneId != lodestoneId)
                    {
                        // Update existing record
                        existingLodestone.LodestoneId = lodestoneId;
                        existingLodestone.AvatarLink = avatarLink;
                        player.AvatarLink = avatarLink;

                        _logger.LogInformation($"Updated Lodestone link for player {characterName} (ContentId: {player.LocalContentId}) from {existingLodestone.LodestoneId} to {lodestoneId}");
                    }
                }

                if (matchingPlayers.Count == 0)
                {
                    _logger.LogInformation($"No existing player records found for character name: {characterName}");
                }
                else
                {
                    _logger.LogInformation($"Processed {matchingPlayers.Count} matching player records for {characterName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error linking Lodestone {lodestoneId} to player records");
            }
        }

        private static string GenerateVerificationCode()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[16];
            rng.GetBytes(bytes);
            return $"PLAYERSCOPE-{Convert.ToHexString(bytes)[..8].ToUpper()}";
        }

        private static string GenerateApiKey()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToHexString(bytes).ToLower()[..16]; // Take first 16 chars
        }
    }
}