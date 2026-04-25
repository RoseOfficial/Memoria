using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Memoria.API.Abstractions.Services;
using Memoria.API.Models.Requests.User;
using Memoria.API.Models.Responses.User;
using Memoria.API.Models.Shared;
using Memoria.API.Constants;
using Memoria.API.Security;
using Memoria.Properties;

namespace Memoria.API.Services
{
    /// <summary>
    /// Service for managing user authentication and profile operations with enhanced security features.
    /// 
    /// Security Enhancements:
    /// - Encrypted storage and transmission of sensitive data
    /// - Secure API key handling with automatic obfuscation in logs
    /// - Protected server-sent event processing
    /// - Secure POST-based authentication flow
    /// - Memory-safe sensitive data handling
    /// </summary>
    public class UserAuthService : BaseApiService, IUserAuthService
    {
        private readonly SecureLogger _secureLogger;
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
            : base(restClient, config, logger)
        {
            _secureLogger = new SecureLogger(logger);
        }

        /// <summary>
        /// Initiates Discord OAuth authentication flow
        /// </summary>
        public async Task<(User? User, string Message)> DiscordAuthAsync(UserRegister register)
        {
            var result = await DiscordAuthAsync(register, CancellationToken.None);
            if (result.Success)
            {
                return (result.Data.User, result.Data.ApiKey);
            }
            return (null, result.Error ?? "Authentication failed");
        }

        /// <summary>
        /// Initiates Discord OAuth authentication flow with cancellation support and enhanced security.
        /// Uses encrypted data transmission and secure API key handling.
        /// </summary>
        public async Task<ApiResponse<(User User, string ApiKey)>> DiscordAuthAsync(UserRegister register, CancellationToken cancellationToken = default)
        {
            if (register == null)
                return ApiResponse<(User User, string ApiKey)>.Fail("Register data cannot be null", 400);

            IsLoggingIn = true;
            SecureApiKey? secureApiKey = null;
            
            try
            {
                _secureLogger.LogInformation("Starting Discord authentication for user: {UserName}", 
                    SecureDataHandler.ObfuscateApiKey(register.Name));

                // Encrypt sensitive registration data instead of using Base64
                var serializedData = JsonConvert.SerializeObject(register);
                var encryptedData = SecureDataHandler.EncryptSensitiveData(serializedData);
                
                // Use POST request with encrypted body instead of GET with URL parameters
                var authRequest = new RestRequest(ApiEndpoints.DISCORD_AUTH, Method.Post);
                authRequest.AddJsonBody(new { encryptedData });
                
                var authResponse = await RestClient.ExecuteAsync(authRequest, cancellationToken);
                
                if (!authResponse.IsSuccessful)
                {
                    _secureLogger.LogWarning("Discord auth initiation failed with status: {StatusCode}", 
                        authResponse.StatusCode);
                    return ApiResponse<(User User, string ApiKey)>.Fail(
                        "Authentication initiation failed", (int)authResponse.StatusCode);
                }

                // Parse the auth URL from response instead of constructing it
                var authUrlData = JsonConvert.DeserializeObject<dynamic>(authResponse.Content!);
                AuthUrl = authUrlData?.authUrl?.ToString() ?? "";
                var sessionId = authUrlData?.sessionId?.ToString() ?? "";

                if (string.IsNullOrEmpty(AuthUrl) || string.IsNullOrEmpty(sessionId))
                {
                    _secureLogger.LogError("Invalid authentication response - missing authUrl or sessionId");
                    return ApiResponse<(User User, string ApiKey)>.Fail("Invalid authentication response", 500);
                }

                Utils.TryOpenURI(new Uri(AuthUrl));

                // Use secure polling endpoint with session ID instead of data parameter
                var pollRequest = new RestRequest($"{ApiEndpoints.WAIT_FOR_LOGIN}?sessionId={sessionId}");
                var response = await RestClient.ExecuteAsync(pollRequest, cancellationToken);
                
                if (!response.IsSuccessful)
                {
                    _secureLogger.LogWarning("Auth polling failed with status: {StatusCode}", response.StatusCode);
                    return ApiResponse<(User User, string ApiKey)>.Fail(
                        "Authentication polling failed", (int)response.StatusCode);
                }

                using var stream = new MemoryStream(response.RawBytes!);
                using var reader = new StreamReader(stream);
                
                while (IsLoggingIn && !cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;
                    
                    if (line.StartsWith("data:"))
                    {
                        var messageData = line.Substring("data:".Length).Trim();
                        
                        // Parse structured JSON response instead of plain text parsing
                        try
                        {
                            var eventData = JsonConvert.DeserializeObject<dynamic>(messageData);
                            var eventType = eventData?.type?.ToString();
                            
                            if (eventType == "login_success")
                            {
                                IsLoggingIn = false;
                                
                                // Securely extract encrypted API key
                                var encryptedApiKey = eventData?.encryptedApiKey?.ToString();
                                if (string.IsNullOrEmpty(encryptedApiKey))
                                {
                                    _secureLogger.LogError("Login successful but no encrypted API key received");
                                    return ApiResponse<(User User, string ApiKey)>.Fail(
                                        "Authentication completed but API key not received", 500);
                                }
                                
                                // Decrypt the API key
                                var decryptedApiKey = SecureDataHandler.DecryptSensitiveData(encryptedApiKey);
                                
                                // Validate the API key
                                if (!SecureDataHandler.IsValidApiKey(decryptedApiKey))
                                {
                                    _secureLogger.LogError("Received invalid API key format");
                                    SecureDataHandler.SecureClearString(decryptedApiKey);
                                    return ApiResponse<(User User, string ApiKey)>.Fail(
                                        "Invalid API key received", 500);
                                }

                                // Create secure API key wrapper
                                secureApiKey = new SecureApiKey(decryptedApiKey);
                                SecureDataHandler.SecureClearString(decryptedApiKey);
                                
                                var user = new User 
                                { 
                                    BaseUrl = Config.BaseUrl,
                                    Name = register.Name,
                                    GameAccountId = register.GameAccountId,
                                    LocalContentId = register.UserLocalContentId
                                };
                                
                                _secureLogger.LogAuthenticationEvent("Discord OAuth", register.Name, true, 
                                    $"API Key: {secureApiKey.GetObfuscatedKey()}");
                                
                                var plainApiKey = secureApiKey.GetPlaintextKey();
                                return ApiResponse<(User User, string ApiKey)>.Ok((user, plainApiKey));
                            }
                            else if (eventType == "login_failed")
                            {
                                IsLoggingIn = false;
                                var errorMessage = eventData?.message?.ToString() ?? "Authentication failed";
                                _secureLogger.LogAuthenticationEvent("Discord OAuth", register.Name, false, errorMessage);
                                return ApiResponse<(User User, string ApiKey)>.Fail(errorMessage, ErrorCodes.UNAUTHORIZED);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _secureLogger.LogWarning("Failed to parse authentication event data: {Error}", ex.Message);
                            // Continue polling for valid events
                        }
                    }
                }

                _secureLogger.LogAuthenticationEvent("Discord OAuth", register.Name, false, "Timeout or cancellation");
                return ApiResponse<(User User, string ApiKey)>.Fail(ErrorCodes.AUTHENTICATION_FAILED_MESSAGE, ErrorCodes.UNAUTHORIZED);
            }
            catch (OperationCanceledException)
            {
                _secureLogger.LogAuthenticationEvent("Discord OAuth", register.Name, false, "Operation cancelled");
                return ApiResponse<(User User, string ApiKey)>.Fail(ErrorCodes.OPERATION_CANCELLED_MESSAGE, ErrorCodes.OPERATION_CANCELLED);
            }
            catch (Exception ex)
            {
                _secureLogger.LogError(ex, "Error during Discord authentication for user: {UserName}", 
                    SecureDataHandler.ObfuscateApiKey(register.Name));
                return ApiResponse<(User User, string ApiKey)>.Fail($"{Loc.ApiError} Authentication failed", 500);
            }
            finally
            {
                IsLoggingIn = false;
                secureApiKey?.Dispose();
            }
        }

        /// <summary>
        /// Performs direct user login using username/password or API key authentication
        /// </summary>
        public async Task<(User? User, string Message)> UserLoginAsync(UserRegister loginUser)
        {
            var result = await UserLoginAsync(loginUser, CancellationToken.None);
            if (result.Success)
            {
                return (result.Data, "Logged in successfully.");
            }
            return (null, result.Error ?? "Login failed");
        }

        /// <summary>
        /// Performs direct user login using username/password or API key authentication with cancellation support
        /// </summary>
        public async Task<ApiResponse<User>> UserLoginAsync(UserRegister loginUser, CancellationToken cancellationToken = default)
        {
            try
            {
                if (loginUser == null)
                    return ApiResponse<User>.Fail(ErrorCodes.PARAMETER_CANNOT_BE_NULL_MESSAGE, ErrorCodes.BAD_REQUEST);

                var request = new RestRequest(ApiEndpoints.USERS_LOGIN);
                request.AddJsonBody(loginUser);

                var response = await RestClient.ExecutePostAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var user = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (user != null)
                    {
                        return ApiResponse<User>.Ok(user, (int)response.StatusCode);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return ApiResponse<User>.Fail(errorMessage, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _secureLogger.LogError(ex, "Error during user login");
                return HandleCommonException<User>(ex, "user login");
            }
        }

        /// <summary>
        /// Updates user profile information on the server
        /// </summary>
        public async Task<(User? User, string Message)> UserUpdateAsync(UserUpdateDto config)
        {
            var result = await UserUpdateAsync(config, CancellationToken.None);
            if (result.Success)
            {
                return (result.Data, Loc.StConfigSaved);
            }
            return (null, result.Error ?? "Update failed");
        }

        /// <summary>
        /// Updates user profile information on the server with cancellation support
        /// </summary>
        public async Task<ApiResponse<User>> UserUpdateAsync(UserUpdateDto updateDto, CancellationToken cancellationToken = default)
        {
            try
            {
                if (updateDto == null)
                    return ApiResponse<User>.Fail(ErrorCodes.PARAMETER_CANNOT_BE_NULL_MESSAGE, ErrorCodes.BAD_REQUEST);

                var request = new RestRequest(ApiEndpoints.USERS_UPDATE)
                    .AddHeader(ApiHeaders.API_KEY, Config.Key ?? string.Empty)
                    .AddHeader(ApiHeaders.VERSION, Utils.clientVer)
                    .AddHeader(ApiHeaders.LANGUAGE, Config.Language);
                    
                request.AddJsonBody(updateDto);
                var response = await RestClient.ExecutePostAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                {
                    var user = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (user != null)
                    {
                        return ApiResponse<User>.Ok(user, (int)response.StatusCode);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return ApiResponse<User>.Fail(errorMessage, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _secureLogger.LogError(ex, "Error while updating user profile");
                return HandleCommonException<User>(ex, "updating user profile");
            }
        }

        /// <summary>
        /// Refreshes the current user's profile information from the server
        /// </summary>
        public async Task<(User? User, string Message)> UserRefreshMyInfoAsync()
        {
            var result = await UserRefreshMyInfoAsync(CancellationToken.None);
            if (result.Success)
            {
                return (result.Data, Loc.ApiProfileRefreshed);
            }
            return (null, result.Error ?? "Refresh failed");
        }

        /// <summary>
        /// Refreshes the current user's profile information from the server with cancellation support
        /// </summary>
        public async Task<ApiResponse<User>> UserRefreshMyInfoAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new RestRequest(ApiEndpoints.USERS_ME)
                    .AddHeader(ApiHeaders.API_KEY, Config.Key ?? string.Empty)
                    .AddHeader(ApiHeaders.VERSION, Utils.clientVer)
                    .AddHeader(ApiHeaders.LANGUAGE, Config.Language);
                    
                var response = await RestClient.ExecuteGetAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var user = JsonConvert.DeserializeObject<User>(response.Content!);
                    if (user != null)
                    {
                        return ApiResponse<User>.Ok(user, (int)response.StatusCode);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return ApiResponse<User>.Fail(errorMessage, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _secureLogger.LogError(ex, "Error while refreshing user info");
                return HandleCommonException<User>(ex, "refreshing user info");
            }
        }

        /// <summary>
        /// Claims ownership of a Lodestone character profile
        /// </summary>
        public async Task<(ClaimLodestoneCharacterDto? LodestoneProfile, string Message)> ClaimLodestoneProfileAsync(string lodestoneProfileLink, int state)
        {
            var result = await ClaimLodestoneProfileAsync(lodestoneProfileLink, state, CancellationToken.None);
            if (result.Success)
            {
                return (result.Data, result.Data?.Message ?? "Profile claimed successfully");
            }
            return (null, result.Error ?? "Claim failed");
        }

        /// <summary>
        /// Claims ownership of a Lodestone character profile with cancellation support
        /// </summary>
        public async Task<ApiResponse<ClaimLodestoneCharacterDto>> ClaimLodestoneProfileAsync(string lodestoneProfileUrl, int claimState, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(lodestoneProfileUrl))
                    return ApiResponse<ClaimLodestoneCharacterDto>.Fail(ErrorCodes.PARAMETER_CANNOT_BE_NULL_MESSAGE, ErrorCodes.BAD_REQUEST);

                if (!Uri.IsWellFormedUriString(lodestoneProfileUrl, UriKind.Absolute))
                    return ApiResponse<ClaimLodestoneCharacterDto>.Fail(ErrorCodes.INVALID_URL_MESSAGE, ErrorCodes.BAD_REQUEST);

                var request = new RestRequest(ApiEndpoints.USERS_LODESTONE_CLAIM)
                    .AddHeader(ApiHeaders.API_KEY, Config.Key ?? string.Empty)
                    .AddHeader(ApiHeaders.VERSION, Utils.clientVer)
                    .AddHeader(ApiHeaders.LANGUAGE, Config.Language);
                    
                request.AddQueryParameter("url", lodestoneProfileUrl);
                request.AddQueryParameter("state", claimState);
                var response = await RestClient.ExecutePostAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessful)
                {
                    var lodestoneProfile = JsonConvert.DeserializeObject<ClaimLodestoneCharacterDto>(response.Content!);
                    if (lodestoneProfile != null)
                    {
                        return ApiResponse<ClaimLodestoneCharacterDto>.Ok(lodestoneProfile, (int)response.StatusCode);
                    }
                }

                var errorMessage = GetErrorMessage(response);
                return ApiResponse<ClaimLodestoneCharacterDto>.Fail(errorMessage, (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _secureLogger.LogError(ex, "Error while claiming Lodestone profile");
                return HandleCommonException<ClaimLodestoneCharacterDto>(ex, "claiming Lodestone profile");
            }
        }

        // GetErrorMessage method is inherited from BaseApiService

        /// <summary>
        /// Generates a one-time link code via POST /v1/auth/link/generate. The user pastes the
        /// returned code into memoria.gg/me/link to merge their Discord identity onto this plugin
        /// install. The server short-circuits with 503 when Discord OAuth isn't configured.
        /// </summary>
        public async Task<ApiResponse<LinkGenerateResponse>> GenerateWebLinkCodeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var apiKey = Config.Key;
                if (string.IsNullOrWhiteSpace(apiKey))
                    return ApiResponse<LinkGenerateResponse>.Fail("Plugin is not authenticated yet — restart the plugin or check your settings.", ErrorCodes.UNAUTHORIZED);

                var request = new RestRequest(ApiEndpoints.AUTH_LINK_GENERATE)
                    .AddHeader(ApiHeaders.API_KEY, apiKey);

                var response = await RestClient.ExecutePostAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessful && !string.IsNullOrWhiteSpace(response.Content))
                {
                    var parsed = JsonConvert.DeserializeObject<LinkGenerateResponse>(response.Content!);
                    if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Code))
                        return ApiResponse<LinkGenerateResponse>.Ok(parsed, (int)response.StatusCode);
                }

                if ((int)response.StatusCode == 503)
                    return ApiResponse<LinkGenerateResponse>.Fail("Server has not configured Discord login yet — try again later.", 503);

                return ApiResponse<LinkGenerateResponse>.Fail(GetErrorMessage(response), (int)response.StatusCode);
            }
            catch (Exception ex)
            {
                _secureLogger.LogError(ex, "Error while generating web link code");
                return HandleCommonException<LinkGenerateResponse>(ex, "generating web link code");
            }
        }

        /// <summary>
        /// Cancels any in-progress authentication operation
        /// </summary>
        public Task<ApiResponse> CancelAuthenticationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                IsLoggingIn = false;
                return Task.FromResult(ApiResponse.Ok());
            }
            catch (Exception ex)
            {
                return Task.FromResult(HandleCommonException(ex, "cancelling authentication"));
            }
        }

        #region Overridden BaseApiService Methods

        /// <summary>
        /// Creates service diagnostics with authentication-specific status information
        /// </summary>
        protected override ServiceDiagnostics CreateServiceDiagnostics()
        {
            return new ServiceDiagnostics
            {
                IsHealthy = !string.IsNullOrWhiteSpace(Config.BaseUrl),
                Status = IsLoggingIn ? "Authenticating" : "Ready",
                ServiceStartTime = DateTime.UtcNow.AddHours(-1), // Placeholder
                ConfigurationValid = !string.IsNullOrWhiteSpace(Config.BaseUrl)
            };
        }

        /// <summary>
        /// Disposes resources including the HttpClient
        /// </summary>
        public override void Dispose()
        {
            _httpClient?.Dispose();
            base.Dispose();
        }

        #endregion
    }
}