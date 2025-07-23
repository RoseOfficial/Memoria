using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlayerScopeServer.Data;
using PlayerScopeServer.Models.DTOs;
using PlayerScopeServer.Models.Entities;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace PlayerScopeServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly PlayerScopeDbContext _context;
        private readonly ILogger<AuthController> _logger;
        
        // Store login sessions temporarily
        private static readonly ConcurrentDictionary<string, UserRegister> _loginSessions = new();
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _loginWaiters = new();

        public AuthController(PlayerScopeDbContext context, ILogger<AuthController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("DiscordAuth")]
        public IActionResult DiscordAuth()
        {
            try
            {
                // Try to get data from query parameter first, then from the raw query string
                string data = Request.Query["data"].FirstOrDefault() ?? "";
                
                // If not found in query params, check if it's appended directly to the URL
                if (string.IsNullOrEmpty(data))
                {
                    var queryString = Request.QueryString.Value;
                    if (!string.IsNullOrEmpty(queryString) && queryString.StartsWith("?"))
                    {
                        data = queryString.Substring(1); // Remove the '?' prefix
                    }
                }
                
                _logger.LogInformation($"DiscordAuth called with data parameter: {(string.IsNullOrEmpty(data) ? "NULL/EMPTY" : "PROVIDED")}");
                
                if (string.IsNullOrEmpty(data))
                {
                    return BadRequest("The data field is required");
                }

                // Decode the user data from PlayerScope
                var decodedBytes = Convert.FromBase64String(data);
                var decodedString = Encoding.UTF8.GetString(decodedBytes);
                var userRegister = JsonSerializer.Deserialize<UserRegister>(decodedString);

                if (userRegister == null)
                {
                    return BadRequest("Invalid user data");
                }

                // Store the session for later completion
                _loginSessions[data] = userRegister;
                _loginWaiters[data] = new TaskCompletionSource<bool>();

                // Create HTML page that redirects to Discord and allows login completion
                var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>PlayerScope Login</title>
    <style>
        body {{ font-family: Arial, sans-serif; max-width: 600px; margin: 50px auto; padding: 20px; text-align: center; }}
        .container {{ background: #f5f5f5; padding: 30px; border-radius: 10px; }}
        .discord-link {{ background: #5865F2; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 20px; }}
        .complete-btn {{ background: #57F287; color: white; padding: 15px 30px; border: none; border-radius: 5px; font-size: 16px; cursor: pointer; margin: 20px; }}
        .complete-btn:hover {{ background: #4AC776; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>Welcome to PlayerScope!</h2>
        <p>To complete your login, please:</p>
        <ol>
            <li>Join our Discord server (optional but recommended)</li>
            <li>Click 'Complete Login' below</li>
        </ol>
        
        <a href='https://discord.gg/qusTut4mPF' target='_blank' class='discord-link'>
            Join Discord Server
        </a>
        <br>
        
        <button onclick='completeLogin()' class='complete-btn'>
            Complete Login
        </button>
        
        <p><strong>Character:</strong> {userRegister.Name}<br>
        <strong>Account ID:</strong> {userRegister.GameAccountId}</p>
    </div>

    <script>
        function completeLogin() {{
            fetch('/auth/complete-login?data={data}', {{ method: 'POST' }})
                .then(response => {{
                    if (response.ok) {{
                        document.body.innerHTML = '<div class=""container""><h2>Login Successful!</h2><p>You can now close this window and return to the game.</p></div>';
                    }}
                }})
                .catch(err => console.error('Error:', err));
        }}
    </script>
</body>
</html>";

                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Discord auth");
                return BadRequest("Invalid request");
            }
        }

        [HttpPost("complete-login")]
        public async Task<IActionResult> CompleteLogin([FromQuery] string data)
        {
            try
            {
                if (!_loginSessions.TryGetValue(data, out var userRegister))
                {
                    return BadRequest("Invalid or expired login session");
                }

                // Create or update user
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.GameAccountId == userRegister.GameAccountId);

                ApplicationUser user;

                if (existingUser == null)
                {
                    // Create new user with same logic as login endpoint
                    var apiKey = GenerateApiKey();
                    
                    user = new ApplicationUser
                    {
                        GameAccountId = userRegister.GameAccountId,
                        PrimaryCharacterLocalContentId = userRegister.UserLocalContentId,
                        Name = userRegister.Name,
                        ApiKey = $"{apiKey}-{userRegister.GameAccountId}",
                        AppRoleId = (int)UserRole.Member,
                        BaseUrl = HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/v1/",
                        CreatedAt = DateTime.UtcNow,
                        LastLoginAt = DateTime.UtcNow
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    user = existingUser;
                    user.LastLoginAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                // Signal the waiting PlayerScope client
                if (_loginWaiters.TryRemove(data, out var tcs))
                {
                    tcs.SetResult(true);
                }

                return Ok(new { message = "Login completed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing login");
                return StatusCode(500, "Error completing login");
            }
        }

        [HttpGet("waitforlogin")]
        public async Task WaitForLogin([FromQuery] string data, CancellationToken cancellationToken)
        {
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            try
            {
                if (!_loginWaiters.TryGetValue(data, out var tcs))
                {
                    await Response.WriteAsync("data: Login session not found\\n\\n");
                    return;
                }

                // Wait for login completion or timeout
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == tcs.Task && tcs.Task.Result)
                {
                    // Get the user data that was created during login
                    if (_loginSessions.TryGetValue(data, out var userRegister))
                    {
                        var user = await _context.Users
                            .FirstOrDefaultAsync(u => u.GameAccountId == userRegister.GameAccountId);
                            
                        if (user != null)
                        {
                            // Send back the user info that PlayerScope expects
                            await Response.WriteAsync($"data: Login successful - API Key: {user.ApiKey}\\n\\n");
                        }
                        else
                        {
                            await Response.WriteAsync("data: Login successful\\n\\n");
                        }
                    }
                    else
                    {
                        await Response.WriteAsync("data: Login successful\\n\\n");
                    }
                    await Response.Body.FlushAsync();
                }
                else
                {
                    await Response.WriteAsync("data: Login timeout\\n\\n");
                    await Response.Body.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in wait for login");
                await Response.WriteAsync("data: Login error\\n\\n");
                await Response.Body.FlushAsync();
            }
        }

        private static string GenerateApiKey()
        {
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToHexString(bytes).ToLower()[..16]; // Take first 16 chars
        }
    }
}