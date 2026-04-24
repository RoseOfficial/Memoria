using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlphaScopeServer.Data;
using AlphaScopeServer.Models.DTOs;
using AlphaScopeServer.Models.Entities;
using AlphaScopeServer.Services.World;
using System.Security.Cryptography;

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

                    // Use the navigation property so EF patches UserId after the user row is
                    // inserted and the identity column is populated. Setting UserId = user.Id
                    // directly would capture 0 and trip the FK constraint on SaveChanges.
                    var primaryChar = new UserCharacter
                    {
                        User = user,
                        LocalContentId = request.UserLocalContentId,
                        Name = request.Name
                    };

                    _context.UserCharacters.Add(primaryChar);
                    await _context.SaveChangesAsync();

                    // Reload with relationships
                    user = await _context.Users
                        .Include(u => u.Characters)
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
                    GameAccountId = user.GameAccountId ?? 0,
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
                    }
                };

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login");
                // Include the exception type + message in the response so plugin logs are
                // self-diagnostic. The login endpoint is exempt from auth and doesn't take any
                // sensitive input, so echoing the server exception is safe.
                return StatusCode(500, new
                {
                    error = "Error during login",
                    type = ex.GetType().Name,
                    detail = ex.Message,
                    inner = ex.InnerException?.Message,
                });
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
                    GameAccountId = user.GameAccountId ?? 0,
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
                    }
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

#if DEBUG
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
#endif

        [HttpGet("me/admin")]
        public Task<IActionResult> GetAdminFlag()
        {
            var viewerUserId = HttpContext.Items["ViewerUserId"] as int?;
            if (viewerUserId is null) return Task.FromResult<IActionResult>(Unauthorized());
            var isAdmin = (bool)(HttpContext.Items["IsAdmin"] ?? false);
            return Task.FromResult<IActionResult>(Ok(new { IsAdmin = isAdmin }));
        }

        [HttpGet("me/contributions")]
        public async Task<IActionResult> GetContributions()
        {
            var viewerUserId = HttpContext.Items["ViewerUserId"] as int?;
            if (viewerUserId is null) return Unauthorized();

            var user = await _context.Users.FindAsync(viewerUserId.Value);
            if (user is null) return Unauthorized();

            // Plan 0c: Recent list is a placeholder — empty array until scan attribution lands.
            // ApplicationUser.TotalContributions is maintained by PlayersController on scan upload.
            var response = new ContributionsResponse(
                Lifetime: user.TotalContributions,
                Recent: Array.Empty<RecentContribution>());
            return Ok(response);
        }

        [HttpGet("me/characters")]
        public async Task<IActionResult> GetCharacters()
        {
            var viewerUserId = HttpContext.Items["ViewerUserId"] as int?;
            if (viewerUserId is null) return Unauthorized();

            var rows = await _context.Players
                .Where(p => p.ClaimedByUserId == viewerUserId.Value)
                .Select(p => new {
                    LocalContentId = p.LocalContentId,
                    Name = p.Name,
                    HomeWorldId = p.HomeWorldId,
                    AvatarUrl = p.AvatarLink,
                    HideAlts = p.HideAlts,
                    HideEncounters = p.HideEncounters,
                    HideEntirely = p.HideEntirely,
                    ClaimedAt = p.ClaimedAt,
                })
                .ToListAsync();

            // Project world name in memory — WorldNames.Resolve is not EF-translatable
            var items = rows.Select(p => new {
                localContentId = p.LocalContentId,
                name = p.Name,
                worldSlug = p.HomeWorldId.HasValue ? WorldNames.ToSlug(WorldNames.Resolve(p.HomeWorldId) ?? "unknown") : "unknown",
                worldName = p.HomeWorldId.HasValue ? WorldNames.Resolve(p.HomeWorldId) ?? "Unknown" : "Unknown",
                avatarUrl = p.AvatarUrl,
                hideAlts = p.HideAlts,
                hideEncounters = p.HideEncounters,
                hideEntirely = p.HideEntirely,
                claimedAt = p.ClaimedAt,
            }).ToList();

            return Ok(items);
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