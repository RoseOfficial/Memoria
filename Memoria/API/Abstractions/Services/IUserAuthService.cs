using System.Threading;
using System.Threading.Tasks;
using Memoria.API.Models.Requests.User;
using Memoria.API.Models.Responses.User;
using Memoria.API.Models.Shared;

namespace Memoria.API.Abstractions.Services
{
    /// <summary>
    /// Interface for managing user authentication and profile operations.
    /// Provides methods for Discord OAuth, direct login, profile management, and Lodestone character claiming.
    /// </summary>
    public interface IUserAuthService
    {
        /// <summary>
        /// Flag indicating whether a Discord OAuth login operation is currently in progress.
        /// Used to prevent concurrent login attempts and provide UI feedback during authentication flow.
        /// </summary>
        bool IsLoggingIn { get; }
        
        /// <summary>
        /// Generated authentication URL for the Discord OAuth flow.
        /// Contains the encoded user registration data and is used to open the browser for authentication.
        /// </summary>
        string AuthUrl { get; }

        /// <summary>
        /// Initiates a Discord OAuth authentication flow for user registration and login.
        /// Opens the user's default browser to the Discord OAuth page and waits for authentication completion.
        /// Uses Server-Sent Events to receive real-time authentication status updates from the server.
        /// The authentication process includes automatic API key generation upon successful login.
        /// </summary>
        /// <param name="register">User registration data containing name, game account details, and content ID</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the authenticated user data and generated API key.
        /// The API key is included in the response message for configuration purposes.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when register is null</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when another login is already in progress</exception>
        /// <exception cref="System.Net.Http.HttpRequestException">Thrown when network communication fails</exception>
        Task<ApiResponse<(User User, string ApiKey)>> DiscordAuthAsync(
            UserRegister register, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs direct user authentication using username/password or API key credentials.
        /// Provides an alternative to Discord OAuth for users who prefer traditional login methods.
        /// Supports both password-based authentication and API key authentication depending on the provided credentials.
        /// </summary>
        /// <param name="loginUser">User login credentials containing either username/password or API key information</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the authenticated user information.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when loginUser is null</exception>
        /// <exception cref="System.ArgumentException">Thrown when required login credentials are missing</exception>
        /// <exception cref="System.UnauthorizedAccessException">Thrown when credentials are invalid</exception>
        Task<ApiResponse<User>> UserLoginAsync(
            UserRegister loginUser, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the current user's profile information on the server.
        /// Used to synchronize local configuration changes with the server-side user profile.
        /// Supports updating user preferences, settings, and other profile-related data.
        /// </summary>
        /// <param name="updateDto">Updated user configuration data with the changes to apply</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the updated user profile information.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when updateDto is null</exception>
        /// <exception cref="System.UnauthorizedAccessException">Thrown when the user is not authenticated</exception>
        /// <exception cref="System.ArgumentException">Thrown when update data validation fails</exception>
        Task<ApiResponse<User>> UserUpdateAsync(
            UserUpdateDto updateDto, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes the current user's profile information from the server.
        /// Used to synchronize server-side changes back to the local client, ensuring data consistency.
        /// Retrieves the latest user profile data including any updates made through other clients or the web interface.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the current user profile information.
        /// </returns>
        /// <exception cref="System.UnauthorizedAccessException">Thrown when the user is not authenticated</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the service is not properly configured</exception>
        Task<ApiResponse<User>> UserRefreshMyInfoAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Claims ownership of a Final Fantasy XIV Lodestone character profile.
        /// Links the user's Memoria account to their official FFXIV Lodestone character page for verification purposes.
        /// Supports multi-step verification processes with different claim states (initiate, verify, complete).
        /// The claiming process may require verification codes or other proof of character ownership.
        /// </summary>
        /// <param name="lodestoneProfileUrl">Complete URL to the Lodestone character profile to claim</param>
        /// <param name="claimState">Current state of the claim process (0=initiate, 1=verify, etc.)</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse with the claim result data and any required next steps.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when lodestoneProfileUrl is null or empty</exception>
        /// <exception cref="System.ArgumentException">Thrown when lodestoneProfileUrl is not a valid URL or claimState is invalid</exception>
        /// <exception cref="System.UnauthorizedAccessException">Thrown when the user is not authenticated</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the character is already claimed by another user</exception>
        Task<ApiResponse<ClaimLodestoneCharacterDto>> ClaimLodestoneProfileAsync(
            string lodestoneProfileUrl, 
            int claimState, 
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels any in-progress authentication operation (Discord OAuth or direct login).
        /// Provides a way to abort long-running authentication processes and reset the service state.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains an ApiResponse indicating whether the cancellation was successful.
        /// </returns>
        Task<ApiResponse> CancelAuthenticationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a one-time code (15-minute expiry) that the user pastes into the web app's
        /// /me/link page to merge their Discord identity onto this plugin install.
        /// </summary>
        Task<ApiResponse<LinkGenerateResponse>> GenerateWebLinkCodeAsync(
            CancellationToken cancellationToken = default);
    }
}