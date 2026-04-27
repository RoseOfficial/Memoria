// Dalamud framework dependencies
using Dalamud.Configuration;
using Dalamud.Interface;
using Dalamud.Plugin;

// Third-party dependencies
using Newtonsoft.Json;

// System dependencies
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;

// Memoria internal dependencies
using Memoria.GUI;
using Memoria.API.Models.Responses.Player;
using Memoria.API.Models.Responses.User;
using Memoria.API.Models.Responses.Server;
using Memoria.API.Models.Responses.Common;
using Memoria.API.Security;
using static Memoria.Handlers.PersistenceContext;

namespace Memoria
{
    /// <summary>
    /// Configuration class for Memoria plugin settings.
    /// Stores user preferences, authentication data, API settings, and UI options.
    /// Automatically persisted by Dalamud's configuration system.
    /// 
    /// Security Features:
    /// - Encrypted storage of API keys and sensitive data
    /// - Automatic obfuscation of sensitive data in logging
    /// - Secure token generation and validation
    /// </summary>
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        /// <summary>
        /// Configuration version for handling config schema updates.
        /// Version 3: Added encrypted API key storage and security enhancements.
        /// Version 4: Added WebBaseUrl and OptOutInGamePopups.
        /// Version 5: Repointed WebBaseUrl from memoria.gg to memoriagg.netlify.app
        ///            (the memoria.gg domain isn't routed to the production deploy yet).
        /// Version 6: Widened AccountId from int to long. The plugin reads
        ///            FFXIV's 64-bit ulong AccountId via unchecked cast; the
        ///            previous int truncation collided distinct accounts.
        /// </summary>
        public int Version { get; set; } = 6;
        
        /// <summary>
        /// Base URL for the MemoriaServer API endpoint. Default points at the production
        /// Render deployment; users on a fork or self-host can override via plugin settings.
        /// </summary>
        public string BaseUrl { get; set; } = "https://alphascope-rvbq.onrender.com/v1/";
        
        /// <summary>
        /// Current user's character name
        /// </summary>
        public string Username { get; set; } = "";
        
        /// <summary>
        /// Current user's FFXIV Content ID (unique character identifier)
        /// </summary>
        public long ContentId { get; set; } = 0;
        
        /// <summary>
        /// Current user's Account ID for API authentication
        /// </summary>
        public long AccountId { get; set; } = 0;
        
        /// <summary>
        /// Encrypted API key for server authentication (randomly generated during registration).
        /// Stored in encrypted format to prevent exposure in config files or memory dumps.
        /// </summary>
        [JsonProperty("EncryptedKey")]
        public string EncryptedKey { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the API key with automatic encryption/decryption.
        /// The actual key is never stored in plaintext.
        /// </summary>
        [JsonIgnore]
        public string Key 
        { 
            get 
            {
                try
                {
                    return string.IsNullOrEmpty(EncryptedKey) ? "" : SecureDataHandler.DecryptSensitiveData(EncryptedKey);
                }
                catch
                {
                    // If decryption fails, return empty string and clear the encrypted data
                    EncryptedKey = "";
                    return "";
                }
            }
            set 
            {
                try
                {
                    EncryptedKey = string.IsNullOrEmpty(value) ? "" : SecureDataHandler.EncryptSensitiveData(value);
                    // Clear the input value from memory (best effort)
                    SecureDataHandler.SecureClearString(value);
                }
                catch
                {
                    // If encryption fails, clear both values for security
                    EncryptedKey = "";
                    throw;
                }
            }
        }
        
        /// <summary>
        /// Whether the user is currently logged into the API server
        /// </summary>
        public bool LoggedIn { get; set; } = true;

        /// <summary>
        /// True once the plugin has completed one-time registration with the central server and
        /// received its API key. Modern keys are opaque random strings (no hyphen, no GameAccountId
        /// suffix); legacy keys had the form {random}-{gameAccountId} but those are no longer issued.
        /// </summary>
        public bool AutoRegistered { get; set; } = false;
        
        /// <summary>
        /// Flag indicating if this is a fresh plugin installation (used for initial setup)
        /// </summary>
        public bool FreshInstall { get; set; } = false;
        
        /// <summary>
        /// User's role ID on the server (for permission management)
        /// </summary>
        public int AppRoleId { get; set; } = 0;
        /// <summary>
        /// Statistics: Total number of player records uploaded to server
        /// </summary>
        public int? UploadedPlayersCount { get; set; } = 0;
        
        /// <summary>
        /// Statistics: Total number of detailed player info records uploaded
        /// </summary>
        public int? UploadedPlayerInfoCount { get; set; } = 0;
        
        /// <summary>
        /// Statistics: Total number of retainer records uploaded to server
        /// </summary>
        public int? UploadedRetainersCount { get; set; } = 0;
        
        /// <summary>
        /// Statistics: Total number of detailed retainer info records uploaded
        /// </summary>
        public int? UploadedRetainerInfoCount { get; set; } = 0;
        
        /// <summary>
        /// Statistics: Total number of player info records fetched from server
        /// </summary>
        public int? FetchedPlayerInfoCount { get; set; } = 0;
        
        /// <summary>
        /// Statistics: Total number of name searches performed
        /// </summary>
        public int? SearchedNamesCount { get; set; } = 0;
        
        /// <summary>
        /// Timestamp of last successful data synchronization with server
        /// </summary>
        public int? LastSyncedTime { get; set; }
        /// <summary>
        /// Thread-safe dictionary of favorited players with their cached information
        /// Key: ContentId, Value: CachedFavoritedPlayer data
        /// </summary>
        public ConcurrentDictionary<long, CachedFavoritedPlayer> FavoritedPlayer = new();
        
        /// <summary>
        /// UI preference: Whether to show detailed timestamps instead of relative dates
        /// </summary>
        public bool bShowDetailedDate { get; set; } = false;
        
        /// <summary>
        /// UI preference: Whether to hide character avatar images in the interface
        /// </summary>
        public bool bHideCharacterAvatars { get; set; } = false;
        
        /// <summary>
        /// User's preferred language for the plugin interface
        /// </summary>
        public LanguageEnum Language { get; set; }
        
        /// <summary>
        /// Whether the user has accepted the terms of service/privacy agreement
        /// </summary>
        public bool AgreementAccepted { get; set; } = false;
        
        /// <summary>
        /// Refresh interval in milliseconds for scanning nearby players (default: 5 seconds)
        /// </summary>
        public int ObjectTableRefreshInterval { get; set; } = 5_000;

        /// <summary>
        /// Base URL of the Memoria web app. Override for dev/staging.
        /// </summary>
        public string WebBaseUrl { get; set; } = "https://memoriagg.netlify.app";

        /// <summary>
        /// Suppress in-game pop-ups (notifications + auto-opening of the micro-card on right-click).
        /// The right-click context menu entry remains; the user just has to click it.
        /// </summary>
        public bool OptOutInGamePopups { get; set; } = false;

        /// <summary>
        /// Running total of player-scan uploads this user has successfully contributed to the
        /// Memoria network over the lifetime of the plugin install. Incremented on each
        /// successful upload batch by the batch size. Starts at 0 on fresh install or after a
        /// config reset; cannot be back-filled from server history.
        /// </summary>
        public long TotalContributions { get; set; } = 0;

        // ========== LODESTONE REFRESH SERVICE SETTINGS ==========
        
        /// <summary>
        /// Whether the background Lodestone refresh service is enabled
        /// </summary>
        public bool LodestoneRefreshEnabled { get; set; } = true;
        
        /// <summary>
        /// Delay in seconds between individual Lodestone refresh requests (default: 1 second)
        /// Processing is now one player per second instead of batches
        /// </summary>
        public int LodestoneRefreshDelaySeconds { get; set; } = 1;
        
        /// <summary>
        /// Number of hours after which a player is considered stale and needs refresh (default: 24 hours)
        /// </summary>
        public int LodestoneStaleThresholdHours { get; set; } = 24;
        
        /// <summary>
        /// Maximum number of players to process in a single refresh batch (default: 10)
        /// NOTE: This setting is deprecated - processing is now one player per second
        /// </summary>
        public int LodestoneRefreshBatchSize { get; set; } = 10;
        
        /// <summary>
        /// Delay in minutes between refresh batches when queue is not empty (default: 0 minutes)
        /// NOTE: This setting is deprecated - processing is now one player per second
        /// </summary>
        public int LodestoneRefreshBatchDelayMinutes { get; set; } = 0;
        
        /// <summary>
        /// Delay in minutes between refresh cycles when queue is empty (default: 0.1 minutes = 6 seconds)
        /// </summary>
        public int LodestoneRefreshIdleDelayMinutes { get; set; } = 0;
        
        /// <summary>
        /// Delay in seconds between refresh cycles when queue is empty (default: 5 seconds)
        /// </summary>
        public int LodestoneRefreshIdleDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Represents a cached favorited player with their basic information and user notes.
        /// This data is stored locally to provide quick access to favorite player information.
        /// </summary>
        public class CachedFavoritedPlayer
        {
            /// <summary>
            /// Player's Account ID for identification across characters
            /// </summary>
            public required ulong? AccountId { get; init; }
            
            /// <summary>
            /// Player's character name at the time of favoriting
            /// </summary>
            public required string Name { get; init; }
            
            /// <summary>
            /// User-defined note about this favorited player
            /// </summary>
            public string? Note { get; set; }
        }
        /// <summary>
        /// Supported languages for the plugin interface
        /// </summary>
        public enum LanguageEnum
        {
            /// <summary>English language</summary>
            en,
            /// <summary>Turkish language</summary>
            tr
        }

        /// <summary>
        /// Generates the authentication token for API requests.
        /// Combines the API key with the account ID in the format: "{Key}-{AccountId}".
        /// The key is automatically decrypted for token generation.
        /// </summary>
        /// <returns>Authentication token string for server requests</returns>
        public string Token()
        {
            var key = Key; // This will decrypt the key
            var token = $"{key}-{AccountId}";
            
            // Clear the decrypted key from memory
            SecureDataHandler.SecureClearString(key);
            
            return token;
        }
        
        /// <summary>
        /// Gets an obfuscated version of the API key safe for logging and display.
        /// </summary>
        /// <returns>Obfuscated API key that's safe to log</returns>
        [JsonIgnore]
        public string ObfuscatedKey
        {
            get
            {
                try
                {
                    var key = Key; // This will decrypt the key
                    var obfuscated = SecureDataHandler.ObfuscateApiKey(key);
                    SecureDataHandler.SecureClearString(key);
                    return obfuscated;
                }
                catch
                {
                    return "[INVALID_KEY]";
                }
            }
        }
        
        /// <summary>
        /// Validates that the stored API key meets security requirements.
        /// </summary>
        /// <returns>True if the API key is valid and secure</returns>
        [JsonIgnore]
        public bool IsApiKeyValid
        {
            get
            {
                try
                {
                    var key = Key; // This will decrypt the key
                    var isValid = SecureDataHandler.IsValidApiKey(key);
                    SecureDataHandler.SecureClearString(key);
                    return isValid;
                }
                catch
                {
                    return false;
                }
            }
        }
        
        /// <summary>
        /// Securely updates the API key with proper validation and encryption.
        /// </summary>
        /// <param name="newApiKey">The new API key to store</param>
        /// <returns>True if the key was successfully updated</returns>
        public bool UpdateApiKey(string newApiKey)
        {
            try
            {
                if (!SecureDataHandler.IsValidApiKey(newApiKey))
                {
                    return false;
                }
                
                Key = newApiKey; // This will encrypt and store the key
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Securely clears the API key from configuration.
        /// </summary>
        public void ClearApiKey()
        {
            try
            {
                // Clear the decrypted key if it exists
                var key = Key;
                SecureDataHandler.SecureClearString(key);
            }
            catch
            {
                // Ignore decryption errors during clearing
            }
            finally
            {
                // Always clear the encrypted storage
                EncryptedKey = "";
            }
        }
        /// <summary>
        /// Saves the current configuration to disk using Dalamud's configuration system.
        /// This method should be called whenever configuration values are changed.
        /// Sensitive data is automatically encrypted before saving.
        /// </summary>
        public void Save()
        {
            try
            {
                // Ensure sensitive data is properly encrypted before saving
                ValidateEncryption();
                Plugin.Instance._pluginInterface.SavePluginConfig(this);
            }
            catch (Exception ex)
            {
                // Log securely without exposing sensitive data
                Plugin.Log?.Error($"Failed to save configuration: {ex.GetType().Name}");
                throw;
            }
        }

        /// <summary>
        /// Saves the current configuration to disk using the provided plugin interface.
        /// Used during initialization when Plugin.Instance may not be available yet.
        /// Sensitive data is automatically encrypted before saving.
        /// </summary>
        public void Save(IDalamudPluginInterface pluginInterface)
        {
            try
            {
                // Ensure sensitive data is properly encrypted before saving
                ValidateEncryption();
                pluginInterface.SavePluginConfig(this);
            }
            catch (Exception ex)
            {
                // Log securely without exposing sensitive data
                Plugin.Log?.Error($"Failed to save configuration with plugin interface: {ex.GetType().Name}");
                throw;
            }
        }
        
        /// <summary>
        /// Validates that sensitive data is properly encrypted before saving.
        /// </summary>
        private void ValidateEncryption()
        {
            // If we have an encrypted key but it's not valid, clear it
            if (!string.IsNullOrEmpty(EncryptedKey))
            {
                try
                {
                    // Test decryption to ensure the encrypted data is valid
                    var testKey = Key;
                    SecureDataHandler.SecureClearString(testKey);
                }
                catch
                {
                    // If decryption fails, clear the invalid encrypted data
                    EncryptedKey = "";
                }
            }
        }

    }

}
