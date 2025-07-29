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

// AlphaScope internal dependencies
using AlphaScope.GUI;
using AlphaScope.API.Models.Player;
using AlphaScope.API.Models.User;
using AlphaScope.API.Models.Server;
using AlphaScope.API.Models.Common;
using static AlphaScope.Handlers.PersistenceContext;

namespace AlphaScope
{
    /// <summary>
    /// Configuration class for AlphaScope plugin settings.
    /// Stores user preferences, authentication data, API settings, and UI options.
    /// Automatically persisted by Dalamud's configuration system.
    /// </summary>
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        /// <summary>
        /// Configuration version for handling config schema updates
        /// </summary>
        public int Version { get; set; } = 2;
        
        /// <summary>
        /// Base URL for the AlphaScopeServer API endpoint
        /// TODO: Update this to production server URL before distribution
        /// </summary>
        public string BaseUrl { get; set; } = "https://localhost:5001/v1/";
        
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
        public int AccountId { get; set; } = 0;
        
        /// <summary>
        /// API key for server authentication (randomly generated during registration)
        /// </summary>
        public string Key { get; set; } = "";
        
        /// <summary>
        /// Whether the user is currently logged into the API server
        /// </summary>
        public bool LoggedIn { get; set; } = true;
        
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
        /// Combines the API key with the account ID in the format: "{Key}-{AccountId}"
        /// </summary>
        /// <returns>Authentication token string for server requests</returns>
        public string Token()
        {
            return $"{Key}-{AccountId}";
        }
        /// <summary>
        /// Saves the current configuration to disk using Dalamud's configuration system.
        /// This method should be called whenever configuration values are changed.
        /// </summary>
        public void Save()
        {
            Plugin.Instance._pluginInterface.SavePluginConfig(this);
        }

        /// <summary>
        /// Saves the current configuration to disk using the provided plugin interface.
        /// Used during initialization when Plugin.Instance may not be available yet.
        /// </summary>
        public void Save(IDalamudPluginInterface pluginInterface)
        {
            pluginInterface.SavePluginConfig(this);
        }
    }

}
