using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UserModels = AlphaScope.API.Models.User;
using AlphaScope.Properties;

namespace AlphaScope.API.Services
{
    /// <summary>
    /// Service for managing user authentication and profile operations
    /// </summary>
    public class UserAuthService : IApiClientBase
    {
        public IRestClient RestClient { get; }
        public Configuration Config { get; }
        public ILogger Logger { get; }
        
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Flag indicating if Discord OAuth login is currently in progress
        /// </summary>
        public bool IsLoggingIn { get; private set; } = false;
        
        /// <summary>
        /// Generated authentication URL for Discord OAuth flow
        /// </summary>
        public string AuthUrl { get; private set; } = string.Empty;

        public UserAuthService(IRestClient restClient, Configuration config, ILogger logger)
        {
            RestClient = restClient;
            Config = config;
            Logger = logger;
        }

        /// <summary>
        /// Initiates Discord OAuth authentication flow
        /// </summary>
        public async Task<(UserModels.User? User, string Message)> DiscordAuthAsync(UserModels.UserRegister register)
        {
            IsLoggingIn = true;
            try
            {
                var output = JsonConvert.SerializeObject(register);
                var bytes = Encoding.UTF8.GetBytes(output);
                var data = Convert.ToBase64String(bytes);
                AuthUrl = Config.BaseUrl.Replace("v1/", "Auth/DiscordAuth?") + data;

                Utils.TryOpenURI(new Uri(AuthUrl));

                var response = await _httpClient.GetAsync(
                    $"{Config.BaseUrl.Replace("v1/", "")}waitforlogin?data={data}", 
                    HttpCompletionOption.ResponseHeadersRead);
                    
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                
                while (IsLoggingIn)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;
                    
                    if (line.StartsWith("data:"))
                    {
                        var message = line.Substring("data:".Length).Trim();
                        if (message.Contains("Login successful"))
                        {
                            IsLoggingIn = false;
                            
                            if (message.Contains("API Key:"))
                            {
                                var apiKeyStart = message.IndexOf("API Key:") + "API Key:".Length;
                                var extractedApiKey = message.Substring(apiKeyStart).Trim();
                                
                                var user = new UserModels.User 
                                { 
                                    BaseUrl = Config.BaseUrl,
                                    Name = register.Name,
                                    GameAccountId = register.GameAccountId,
                                    LocalContentId = register.UserLocalContentId
                                };
                                
                                return (user, extractedApiKey);
                            }
                            break;
                        }
                    }
                }

                return (null, "Authentication failed");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during Discord authentication");
                return (null, $"{Loc.ApiError} {ex.Message}");
            }
            finally
            {
                IsLoggingIn = false;
            }
        }

        /// <summary>
        /// Performs direct user login using username/password or API key authentication
        /// </summary>
        public async Task<(UserModels.User? User, string Message)> UserLoginAsync(UserModels.UserRegister loginUser)
        {
            try
            {
                var request = new RestRequest("users/login");
                request.AddJsonBody(loginUser);

                var response = await RestClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var user = JsonConvert.DeserializeObject<UserModels.User>(response.Content!);
                    if (user != null)
                    {
                        return (user, "Logged in successfully.");
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return (null, errorMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during user login");
                return (null, $"{Loc.ApiError} {ex.Message}");
            }
        }

        /// <summary>
        /// Updates user profile information on the server
        /// </summary>
        public async Task<(UserModels.User? User, string Message)> UserUpdateAsync(UserModels.UserUpdateDto config)
        {
            try
            {
                var request = new RestRequest("users/update")
                    .AddHeader("V", Utils.clientVer)
                    .AddHeader("L", Config.Language);
                    
                request.AddJsonBody(config);
                var response = await RestClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                {
                    var user = JsonConvert.DeserializeObject<UserModels.User>(response.Content!);
                    if (user != null)
                    {
                        return (user, Loc.StConfigSaved);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return (null, errorMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while updating user profile");
                return (null, $"{Loc.ApiError} {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the current user's profile information from the server
        /// </summary>
        public async Task<(UserModels.User? User, string Message)> UserRefreshMyInfoAsync()
        {
            try
            {
                var request = new RestRequest("users/me")
                    .AddHeader("V", Utils.clientVer)
                    .AddHeader("L", Config.Language);
                    
                var response = await RestClient.ExecuteGetAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var user = JsonConvert.DeserializeObject<UserModels.User>(response.Content!);
                    if (user != null)
                    {
                        return (user, Loc.ApiProfileRefreshed);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return (null, errorMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while refreshing user info");
                return (null, $"{Loc.ApiError} {ex.Message}");
            }
        }

        /// <summary>
        /// Claims ownership of a Lodestone character profile
        /// </summary>
        public async Task<(UserModels.ClaimLodestoneCharacterDto? LodestoneProfile, string Message)> ClaimLodestoneProfileAsync(string lodestoneProfileLink, int state)
        {
            try
            {
                var request = new RestRequest("users/lodestone/claim")
                    .AddHeader("V", Utils.clientVer)
                    .AddHeader("L", Config.Language);
                    
                request.AddQueryParameter("url", lodestoneProfileLink);
                request.AddQueryParameter("state", state);
                var response = await RestClient.ExecutePostAsync(request).ConfigureAwait(true);

                if (response.IsSuccessful)
                {
                    var lodestoneProfile = JsonConvert.DeserializeObject<UserModels.ClaimLodestoneCharacterDto>(response.Content!);
                    if (lodestoneProfile != null)
                    {
                        return (lodestoneProfile, lodestoneProfile.Message);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return (null, errorMessage);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while claiming Lodestone profile");
                return (null, $"{Loc.ApiError} {ex.Message}");
            }
        }

        private static string GetErrorMessage(RestResponse response)
        {
            if (!string.IsNullOrWhiteSpace(response.Content))
                return $"{Loc.ApiError} {response.Content}";
            if (response.StatusCode == 0)
                return $"{Loc.ApiError} {Loc.ApiServiceUnavailable}";
            return $"{Loc.ApiError} {response.StatusCode}";
        }
    }
}