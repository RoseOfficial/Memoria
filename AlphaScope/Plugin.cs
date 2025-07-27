// Dalamud framework dependencies for FFXIV plugin integration
using Dalamud.Extensions.MicrosoftLogging;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

// FFXIV client structures for direct game memory access
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

// Microsoft dependencies for dependency injection, logging, and Entity Framework
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// AlphaScope internal components
using AlphaScope.API;
using AlphaScope.Database;
using AlphaScope.GUI;
using AlphaScope.Handlers;
using AlphaScope.Properties;
using AlphaScope.Services;

// System dependencies
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AlphaScope;

/// <summary>
/// Main plugin class for AlphaScope - a FFXIV Dalamud plugin that tracks player data.
/// This class handles plugin initialization, dependency injection setup, database configuration,
/// and coordinates all major plugin subsystems including data collection, API communication, and UI management.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    /// <summary>
    /// SQLite database filename for local data storage
    /// </summary>
    public const string DatabaseFileName = "AlphaScope.data.sqlite3";
    
    /// <summary>
    /// SQLite connection string for database operations
    /// </summary>
    private readonly string _sqliteConnectionString;
    
    /// <summary>
    /// Dependency injection service provider for managing plugin services
    /// </summary>
    public static ServiceProvider? _serviceProvider;
    
    /// <summary>
    /// Dalamud command manager for handling chat commands
    /// </summary>
    private readonly ICommandManager _commandManager;
    
    /// <summary>
    /// Dalamud context menu service for adding right-click menu options
    /// </summary>
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    
    /// <summary>
    /// Dalamud texture provider for loading and managing UI textures
    /// </summary>
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    
    /// <summary>
    /// Dalamud data manager for accessing game data sheets and resources
    /// </summary>
    internal static IDataManager DataManager { get; set; } = null!;
    
    /// <summary>
    /// Dalamud game GUI service for UI interactions and overlays
    /// </summary>
    internal static IGameGui _gameGui { get; set; } = null!;
    
    /// <summary>
    /// Dalamud notification manager for showing in-game notifications
    /// </summary>
    [PluginService] internal static INotificationManager Notification { get; private set; } = null!;
    
    /// <summary>
    /// Singleton instance of the plugin for global access
    /// </summary>
    internal static Plugin Instance { get; private set; } = null!;
    
    /// <summary>
    /// Dalamud logging service for plugin diagnostics
    /// </summary>
    internal static IPluginLog Log { get; private set; } = null!;
    
    /// <summary>
    /// Plugin configuration settings loaded from Dalamud config system
    /// </summary>
    public Configuration Configuration { get; }
    
    /// <summary>
    /// HTTP client for communicating with AlphaScopeServer API
    /// </summary>
    public ApiClient ApiClient { get; set; }
    

    /// <summary>
    /// Modern main window with advanced features and dockable layout
    /// </summary>
    public GUI.Modern.Views.ModernMainWindow ModernMainWindow;

    /// <summary>
    /// Dalamud window system for managing all plugin windows
    /// </summary>
    internal WindowSystem ws;
    
    /// <summary>
    /// Dalamud plugin interface for core plugin functionality
    /// </summary>
    internal IDalamudPluginInterface _pluginInterface {  get; }
    
    /// <summary>
    /// Manager for caching and retrieving character avatar images from Lodestone
    /// </summary>
    public static AvatarCacheManager AvatarCacheManager = null!;
    
    /// <summary>
    /// Background service for continuously refreshing character data from Lodestone
    /// </summary>
    private LodestoneRefreshService? _lodestoneRefreshService;
    /// <summary>
    /// Initializes the AlphaScope plugin with dependency injection from Dalamud.
    /// Sets up the service container, configures database connections, initializes UI windows,
    /// registers chat commands, and starts all data collection handlers.
    /// </summary>
    /// <param name="pluginInterface">Core plugin interface for Dalamud integration</param>
    /// <param name="framework">Game framework service for tick-based operations</param>
    /// <param name="clientState">Client state service for character and world information</param>
    /// <param name="gameGui">Game GUI service for UI interactions</param>
    /// <param name="chatGui">Chat service for sending messages to game chat</param>
    /// <param name="gameInteropProvider">Service for hooking into game functions</param>
    /// <param name="addonLifecycle">Service for monitoring UI addon lifecycle events</param>
    /// <param name="commandManager">Service for registering chat commands</param>
    /// <param name="dataManager">Service for accessing game data sheets</param>
    /// <param name="targetManager">Service for accessing current target information</param>
    /// <param name="objectTable">Service for accessing nearby game objects and players</param>
    /// <param name="marketBoard">Service for monitoring market board interactions</param>
    /// <param name="pluginLog">Logging service for debugging and diagnostics</param>
    /// <param name="contextMenu">Service for adding context menu items</param>
    /// <param name="textureProvider">Service for loading UI textures and images</param>
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        IClientState clientState,
        IGameGui gameGui,
        IChatGui chatGui,
        IGameInteropProvider gameInteropProvider,
        IAddonLifecycle addonLifecycle,
        ICommandManager commandManager,
        IDataManager dataManager,
        ITargetManager targetManager,
        IObjectTable objectTable,
        IMarketBoard marketBoard,
        IPluginLog pluginLog,
        IContextMenu contextMenu,
        ITextureProvider textureProvider)
    {
        // Set up singleton instance and logging
        Instance = this;
        Log = pluginLog;
        
        // Initialize dependency injection container
        ServiceCollection serviceCollection = new();
        
        // Configure logging with Dalamud integration and Entity Framework filtering
        serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace)
            .ClearProviders()
            .AddDalamudLogger(pluginLog)
            .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));
        
        // Register core plugin services
        serviceCollection.AddSingleton<IDalamudPlugin>(this);
        serviceCollection.AddSingleton(pluginInterface);
        serviceCollection.AddSingleton(framework);
        serviceCollection.AddSingleton(clientState);
        serviceCollection.AddSingleton(gameGui);
        serviceCollection.AddSingleton(chatGui);
        serviceCollection.AddSingleton(gameInteropProvider);
        serviceCollection.AddSingleton(addonLifecycle);
        serviceCollection.AddSingleton(commandManager);
        serviceCollection.AddSingleton(dataManager);
        serviceCollection.AddSingleton(targetManager);
        serviceCollection.AddSingleton(objectTable);
        serviceCollection.AddSingleton(marketBoard);
        serviceCollection.AddSingleton(textureProvider);

        // Register AlphaScope-specific services
        serviceCollection.AddSingleton<PersistenceContext>();           // Handles data persistence and API uploads
        serviceCollection.AddSingleton<CWLSHandler>();                  // Handles Cross-World Linkshell data
        serviceCollection.AddSingleton<ObjectTableHandler>();           // Scans nearby players and objects
        serviceCollection.AddSingleton<GameHooks>();                    // Low-level game event hooks
        serviceCollection.AddSingleton<ApiClient>();                    // HTTP client for server communication
        serviceCollection.AddSingleton<LodestoneRefreshService>();       // Background Lodestone data refresh service
        
        // Load plugin configuration from Dalamud config system
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        
        // Migrate configuration to newer versions
        MigrateConfiguration(pluginInterface);
        
        // Register configuration as a service
        serviceCollection.AddSingleton(Configuration);

        // Initialize localization based on user preference or system culture
        if (string.IsNullOrWhiteSpace(Configuration.Language.ToString()))
        {
            // Auto-detect language: Turkish or English (default)
            if (CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "tr")
            { Loc.Culture = new CultureInfo("tr"); Configuration.Language = Configuration.LanguageEnum.tr; }
            else
            { Loc.Culture = new CultureInfo("en"); Configuration.Language = Configuration.LanguageEnum.en; }
            pluginInterface.SavePluginConfig(Configuration);
        }
        else { Loc.Culture = new CultureInfo(Configuration.Language.ToString()); }

        // Generate API key for fresh installations
        if (Configuration.FreshInstall && string.IsNullOrWhiteSpace(Configuration.Key))
        {
            Configuration.FreshInstall = false; 
            Configuration.Key = Utils.GenerateRandomKey();
            pluginInterface.SavePluginConfig(Configuration);
        }


        // Store core services for later use
        _pluginInterface = pluginInterface;
        _commandManager = commandManager;

        // Enable context menu integration
        Handlers.ContextMenu.Enable();
        DataManager = dataManager;
        _gameGui = gameGui;

        // Initialize UI window system and create modern plugin window
        ws = new();
        ModernMainWindow = new();
        
        // Register modern window with the window system
        ws.AddWindow(ModernMainWindow);
        
        // Initialize avatar caching system
        AvatarCacheManager = new AvatarCacheManager();

        // Register UI drawing and event handlers
        pluginInterface.UiBuilder.Draw += ws.Draw;
        pluginInterface.UiBuilder.OpenMainUi += delegate { ModernMainWindow.IsOpen = true; };
        pluginInterface.UiBuilder.OpenConfigUi += delegate { ModernMainWindow.IsOpen = true; };

        // Register chat command for opening the plugin
        _commandManager.AddHandler("/alpha", new CommandInfo(ProcessCommand)
        {
            HelpMessage = Loc.CmOpenUI
        });

        // Set up database connection and build service provider
        _sqliteConnectionString = PrepareSqliteDb(serviceCollection, pluginInterface.GetPluginConfigDirectory());
        _serviceProvider = serviceCollection.BuildServiceProvider();
        ApiClient = _serviceProvider.GetRequiredService<ApiClient>();

        // Initialize database with proper schema and start all handlers
        RunMigrations(_serviceProvider);
        InitializeRequiredServices(_serviceProvider);
    }
    /// <summary>
    /// Processes chat commands for the plugin.
    /// Handles /alpha command to open the modern UI.
    /// </summary>
    /// <param name="command">The command that was executed</param>
    /// <param name="arguments">Arguments passed with the command</param>
    private void ProcessCommand(string command, string arguments)
    {
        if (command == "/alpha")
        {
            if (arguments.Trim().ToLower() == "test")
            {
                // Test the Lodestone refresh service
                Task.Run(async () =>
                {
                    try
                    {
                        Log.Information("Testing Lodestone refresh service...");
                        var success = await (_lodestoneRefreshService?.ForceProcessNextPlayer() ?? Task.FromResult(false));
                        Log.Information($"Test result: {success}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error testing Lodestone service");
                    }
                });
            }
            else
            {
                ModernMainWindow.IsOpen = true;
            }
        }
    }

    /// <summary>
    /// Configures the SQLite database connection and registers the Entity Framework context.
    /// Creates the database file path within the plugin configuration directory.
    /// </summary>
    /// <param name="serviceCollection">Service collection to register the DbContext with</param>
    /// <param name="getPluginConfigDirectory">Plugin configuration directory path</param>
    /// <returns>SQLite connection string for the configured database</returns>
    private static string PrepareSqliteDb(IServiceCollection serviceCollection, string getPluginConfigDirectory)
    {
        string connectionString = $"Data Source={Path.Join(getPluginConfigDirectory, DatabaseFileName)}";
        serviceCollection.AddDbContext<RetainerTrackContext>(o => o
            .UseSqlite(connectionString));
            //.UseModel(RetainerTrackContextModel.Instance)); // Commented out - using standard EF model
        return connectionString;
    }

    /// <summary>
    /// Handles database initialization and schema migrations.
    /// Creates the database with proper nullable schema if it doesn't exist,
    /// or updates existing schema to ensure compatibility with current data model.
    /// This method handles legacy database upgrades and ensures proper nullable foreign keys.
    /// </summary>
    /// <param name="serviceProvider">Service provider for accessing database context</param>
    private static void RunMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
        
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            Log.Information($"Database connection string: {connection.ConnectionString}");
            connection.Open();
            
            using var command = connection.CreateCommand();
            
            // Check if database exists by looking for the Players table
            command.CommandText = @"
                SELECT name FROM sqlite_master WHERE type='table' AND name='Players';
            ";
            
            var result = command.ExecuteScalar();
            bool playersTableExists = result != null;
            Log.Information($"Players table exists: {playersTableExists}");
            
            if (!playersTableExists)
            {
                // Create fresh database with proper nullable schema
                // Players table stores character information with nullable job data
                command.CommandText = @"
                    CREATE TABLE Players (
                        LocalContentId INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        AccountId INTEGER NULL,
                        HomeWorldId INTEGER NULL,
                        CurrentWorldId INTEGER NULL,
                        CurrentJobId INTEGER NULL,
                        CurrentJobLevel INTEGER NULL,
                        AvatarLink TEXT NULL,
                        LastScannedAt TEXT NULL
                    );
                ";
                command.ExecuteNonQuery();
            }
            else
            {
                // Database exists - check if legacy Retainers table exists and remove it
                command.CommandText = @"
                    SELECT name FROM sqlite_master WHERE type='table' AND name='Retainers';
                ";
                
                var retainerTableResult = command.ExecuteScalar();
                if (retainerTableResult != null)
                {
                    // Drop legacy Retainers table since we're removing retainer tracking
                    command.CommandText = "DROP TABLE IF EXISTS Retainers;";
                    command.ExecuteNonQuery();
                }
                
                // Check if Players table has job tracking columns (added in later versions)
                command.CommandText = @"
                    PRAGMA table_info(Players);
                ";
                
                using var playerReader = command.ExecuteReader();
                bool hasJobId = false;
                bool hasJobLevel = false;
                bool hasAvatarLink = false;
                bool hasLastScannedAt = false;
                bool hasHomeWorldId = false;
                bool hasCurrentWorldId = false;
                while (playerReader.Read())
                {
                    var columnName = playerReader.GetString(1); // name column
                    if (columnName == "CurrentJobId") hasJobId = true;
                    if (columnName == "CurrentJobLevel") hasJobLevel = true;
                    if (columnName == "AvatarLink") hasAvatarLink = true;
                    if (columnName == "LastScannedAt") hasLastScannedAt = true;
                    if (columnName == "HomeWorldId") hasHomeWorldId = true;
                    if (columnName == "CurrentWorldId") hasCurrentWorldId = true;
                }
                playerReader.Close();
                
                // Add job tracking columns if they don't exist (legacy database upgrade)
                if (!hasJobId)
                {
                    command.CommandText = "ALTER TABLE Players ADD COLUMN CurrentJobId INTEGER NULL;";
                    command.ExecuteNonQuery();
                }
                
                if (!hasJobLevel)
                {
                    command.CommandText = "ALTER TABLE Players ADD COLUMN CurrentJobLevel INTEGER NULL;";
                    command.ExecuteNonQuery();
                }
                
                // Add avatar link column if it doesn't exist
                if (!hasAvatarLink)
                {
                    command.CommandText = "ALTER TABLE Players ADD COLUMN AvatarLink TEXT NULL;";
                    command.ExecuteNonQuery();
                }
                
                // Add last scanned timestamp column if it doesn't exist (for background refresh system)
                if (!hasLastScannedAt)
                {
                    command.CommandText = "ALTER TABLE Players ADD COLUMN LastScannedAt TEXT NULL;";
                    command.ExecuteNonQuery();
                }
                
                // Add world tracking columns if they don't exist (for player card display)
                if (!hasHomeWorldId)
                {
                    command.CommandText = "ALTER TABLE Players ADD COLUMN HomeWorldId INTEGER NULL;";
                    command.ExecuteNonQuery();
                }
                
                if (!hasCurrentWorldId)
                {
                    command.CommandText = "ALTER TABLE Players ADD COLUMN CurrentWorldId INTEGER NULL;";
                    command.ExecuteNonQuery();
                }
            }
            
            connection.Close();
        }
        catch (Exception ex)
        {
            // Log database setup errors - database is critical for plugin functionality
            System.Console.WriteLine($"Error setting up database: {ex.Message}");
            throw; // Re-throw since the plugin cannot function without database access
        }
    }

    /// <summary>
    /// Initializes all required plugin services that need to start immediately.
    /// These services register event handlers and start background processing.
    /// Order matters - some services depend on others being initialized first.
    /// </summary>
    /// <param name="serviceProvider">Service provider containing registered services</param>
    private void InitializeRequiredServices(ServiceProvider serviceProvider)
    {
        
        // Start social system monitoring
        serviceProvider.GetRequiredService<CWLSHandler>();
        
        // Start player scanning and game event monitoring
        serviceProvider.GetRequiredService<ObjectTableHandler>();
        serviceProvider.GetRequiredService<GameHooks>();
        
        // Start background Lodestone refresh service
        Log.Information("Plugin: Initializing LodestoneRefreshService...");
        _lodestoneRefreshService = serviceProvider.GetRequiredService<LodestoneRefreshService>();
        Log.Information("Plugin: LodestoneRefreshService instance created, starting background task...");
        
        // Start the service immediately in a fire-and-forget manner
        var startTask = _lodestoneRefreshService.StartAsync();
        _ = startTask.ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                Log.Information("Plugin: LodestoneRefreshService.StartAsync() completed successfully");
            }
            else if (task.IsFaulted)
            {
                Log.Error(task.Exception, "Plugin: Failed to start LodestoneRefreshService");
            }
            else
            {
                Log.Warning("Plugin: LodestoneRefreshService.StartAsync() was cancelled");
            }
        }, TaskScheduler.Default);

        // Authentication check removed - users will need to manually configure API keys
    }


    /// <summary>
    /// Disposes of all plugin resources when the plugin is unloaded.
    /// Stops all background services, unregisters event handlers, and cleans up database connections.
    /// This method is called by Dalamud when the plugin is disabled or the game is closing.
    /// </summary>
    public void Dispose()
    {
        // Stop background Lodestone refresh service
        _lodestoneRefreshService?.StopAsync().GetAwaiter().GetResult();
        _lodestoneRefreshService?.Dispose();
        
        // Dispose of dependency injection container and all registered services
        _serviceProvider?.Dispose();
        
        // Clean up handlers and background processes
        Handlers.ContextMenu.Disable();
        PersistenceContext.StopUploads();
        AvatarCacheManager.Dispose();

        // Unregister command handler
        _commandManager.RemoveHandler("/alpha");

        // Unregister UI event handlers
        _pluginInterface.UiBuilder.Draw -= ws.Draw;
        _pluginInterface.UiBuilder.OpenMainUi -= delegate { ModernMainWindow.IsOpen = true; };
        _pluginInterface.UiBuilder.OpenConfigUi -= delegate { ModernMainWindow.IsOpen = true; };

        // Ensure SQLite connection pool is cleared to prevent file locking issues
        using (SqliteConnection sqliteConnection = new(_sqliteConnectionString))
            SqliteConnection.ClearPool(sqliteConnection);
    }

    /// <summary>
    /// Migrates configuration settings from older versions to maintain compatibility
    /// and ensure users get performance improvements automatically
    /// </summary>
    private void MigrateConfiguration(IDalamudPluginInterface pluginInterface)
    {
        bool needsSave = false;
        
        // Migrate from version 1 to version 2: Faster Lodestone refresh settings
        if (Configuration.Version < 2)
        {
            Log.Information("Migrating configuration from version {OldVersion} to version 2", Configuration.Version);
            
            // Update Lodestone refresh timings for much faster avatar processing
            if (Configuration.LodestoneRefreshDelaySeconds > 1)
            {
                Log.Information("Updating LodestoneRefreshDelaySeconds from {Old}s to 1s", Configuration.LodestoneRefreshDelaySeconds);
                Configuration.LodestoneRefreshDelaySeconds = 1;
                needsSave = true;
            }
            
            if (Configuration.LodestoneRefreshIdleDelayMinutes > 1)
            {
                Log.Information("Updating LodestoneRefreshIdleDelayMinutes from {Old}m to 0m", Configuration.LodestoneRefreshIdleDelayMinutes);
                Configuration.LodestoneRefreshIdleDelayMinutes = 0;
                needsSave = true;
            }
            
            // Add the new LodestoneRefreshIdleDelaySeconds setting
            Configuration.LodestoneRefreshIdleDelaySeconds = 5;
            Log.Information("Added LodestoneRefreshIdleDelaySeconds = 5s for faster processing");
            needsSave = true;
            
            Configuration.Version = 2;
            needsSave = true;
            
            Log.Information("Configuration migration to version 2 completed");
        }
        
        // Save configuration if any changes were made
        if (needsSave)
        {
            Configuration.Save(pluginInterface);
            Log.Information("Configuration saved after migration");
        }
    }
}