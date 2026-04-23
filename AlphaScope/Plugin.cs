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

// Microsoft dependencies for dependency injection and logging
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// AlphaScope internal components
using AlphaScope.API;
using AlphaScope.API.Client.Configuration;
using AlphaScope.API.Extensions;
using Microsoft.Extensions.Options;
using AlphaScope.GUI;
using AlphaScope.Handlers;
using AlphaScope.Properties;
using AlphaScope.Services;

// System dependencies
using System;
using System.Globalization;
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
    /// Manager for caching and retrieving minion icons from Lodestone
    /// </summary>
    public static MinionCacheManager MinionCacheManager = null!;
    
    /// <summary>
    /// Manager for caching and retrieving mount icons from Lodestone
    /// </summary>
    public static MountCacheManager MountCacheManager = null!;
    
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
        
        // Configure logging with Dalamud integration
        serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace)
            .ClearProviders()
            .AddDalamudLogger(pluginLog));
        
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
        
        // Add memory cache for API caching
        serviceCollection.AddMemoryCache();

        // Load plugin configuration from Dalamud config system
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        
        // Register AlphaScope API services using extension method
        serviceCollection.AddAlphaScopeApi(Configuration, options =>
        {
            options.EnableLogging = true;
            options.EnableDetailedErrorLogging = true;
            options.TimeoutSeconds = 30;
            options.MaxRetryAttempts = 3;
            options.RetryDelayMilliseconds = 1000;
        });

        // Register AlphaScope-specific game services
        serviceCollection.AddSingleton<PersistenceContext>();           // Handles data persistence and API uploads
        serviceCollection.AddSingleton<CWLSHandler>();                  // Handles Cross-World Linkshell data
        serviceCollection.AddSingleton<ObjectTableHandler>();           // Scans nearby players and objects
        serviceCollection.AddSingleton<GameHooks>();                    // Low-level game event hooks
        serviceCollection.AddSingleton<MinionDataService>();            // Comprehensive minion name-to-ID mapping service
        serviceCollection.AddSingleton<MountDataService>();             // Comprehensive mount name-to-ID mapping service
        serviceCollection.AddSingleton<LodestoneRefreshService>();       // Background Lodestone data refresh service
        serviceCollection.AddSingleton<ICollectiblesAcquisitionService, CollectiblesApiService>(); // FFXIVCollect API service for acquisition data
        
        // Migrate configuration to newer versions
        MigrateConfiguration(pluginInterface);

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
        
        // Initialize minion icon caching system
        MinionCacheManager = new MinionCacheManager();
        
        // Initialize mount icon caching system
        MountCacheManager = new MountCacheManager();

        // Register UI drawing and event handlers
        pluginInterface.UiBuilder.Draw += ws.Draw;
        pluginInterface.UiBuilder.OpenMainUi += delegate { ModernMainWindow.IsOpen = true; };
        pluginInterface.UiBuilder.OpenConfigUi += delegate { ModernMainWindow.IsOpen = true; };

        // Register chat command for opening the plugin
        _commandManager.AddHandler("/alpha", new CommandInfo(ProcessCommand)
        {
            HelpMessage = Loc.CmOpenUI
        });

        // Build service provider and start all handlers. No local DB — the server is the source of truth.
        _serviceProvider = serviceCollection.BuildServiceProvider();
        ApiClient = _serviceProvider.GetRequiredService<ApiClient>();
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

    // PrepareSqliteDb and RunMigrations removed along with the local SQLite layer. The server
    // is the sole source of truth for player data; see PersistenceContext.ReloadCache for how
    // the in-memory cache is hydrated from the API on startup.

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
        _lodestoneRefreshService = serviceProvider.GetRequiredService<LodestoneRefreshService>();
        
        // Start the service immediately in a fire-and-forget manner
        var startTask = _lodestoneRefreshService.StartAsync();
        _ = startTask.ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                // Successful start is the common case; don't log it.
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

        // Auto-register with the server once we know the player's game AccountId. The auth
        // middleware expects keys of the form {random}-{gameAccountId}; only the server can
        // generate one, so on first run we post a login request and persist the returned key.
        _ = Task.Run(() => AutoRegisterWithServerAsync(serviceProvider));
    }

    private async Task AutoRegisterWithServerAsync(IServiceProvider serviceProvider)
    {
        try
        {
            if (Configuration.AutoRegistered && !string.IsNullOrWhiteSpace(Configuration.Key) && Configuration.Key.Contains('-'))
                return;

            var clientState = serviceProvider.GetRequiredService<Dalamud.Plugin.Services.IClientState>();
            var framework = serviceProvider.GetRequiredService<Dalamud.Plugin.Services.IFramework>();

            // Reads of IClientState members must happen on the framework thread; touching them
            // from the thread-pool throws "Not on main thread!". We marshal each poll and the
            // final field-grab through framework.RunOnFrameworkThread, then do the HTTP call
            // back on the thread pool so we don't stall the game.
            (string? Name, int AccountId, long ContentId)? snapshot = null;
            var deadline = DateTime.UtcNow.AddMinutes(30);
            while (DateTime.UtcNow < deadline)
            {
                snapshot = await framework.RunOnFrameworkThread(() =>
                {
                    if (!clientState.IsLoggedIn || clientState.LocalPlayer is null)
                        return ((string?)null, 0, 0L);

                    var lp = clientState.LocalPlayer;
                    var name = lp.Name.TextValue;
                    int accountId = 0;
                    unsafe
                    {
                        var chr = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)lp.Address;
                        if (chr != null && chr->AccountId != 0)
                            accountId = unchecked((int)chr->AccountId);
                    }
                    return (name, accountId, (long)clientState.LocalContentId);
                });

                if (snapshot is { } s && !string.IsNullOrEmpty(s.Name) && s.AccountId != 0)
                    break;

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            if (snapshot is not { } finalSnapshot
                || string.IsNullOrEmpty(finalSnapshot.Name)
                || finalSnapshot.AccountId == 0)
            {
                Log.Warning("AutoRegisterWithServerAsync: gave up waiting for LocalPlayer/AccountId after 30min; will retry on next plugin start");
                return;
            }

            var request = new API.Models.Requests.User.UserRegister
            {
                Name = finalSnapshot.Name!,
                GameAccountId = finalSnapshot.AccountId,
                UserLocalContentId = finalSnapshot.ContentId,
                ClientId = "AlphaScope",
                Version = Utils.clientVer,
            };

            // Retry login a few times — Neon serverless compute can take several seconds to
            // wake from idle, and a single timeout on first contact would otherwise leave the
            // plugin stuck without a valid API key until the next plugin reload.
            API.Client.Result<(API.Models.Responses.User.User? User, string Message)>? result = null;
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                result = await ApiClient.UserLoginAsync(request);
                if (result.IsSuccess && result.Value.User is not null && !string.IsNullOrWhiteSpace(result.Value.User.ApiKey))
                    break;
                Log.Warning("AutoRegisterWithServerAsync: login attempt {Attempt}/5 failed — {Error}",
                    attempt, result?.Error ?? result?.Value.Message ?? "unknown");
                if (attempt < 5)
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 3)); // 3s, 6s, 9s, 12s
            }

            if (result is { IsSuccess: true } && result.Value.User is not null && !string.IsNullOrWhiteSpace(result.Value.User.ApiKey))
            {
                Configuration.Key = result.Value.User.ApiKey!;
                Configuration.AccountId = request.GameAccountId;
                Configuration.ContentId = request.UserLocalContentId;
                Configuration.Username = request.Name;
                Configuration.AutoRegistered = true;
                Configuration.LoggedIn = true;
                _pluginInterface.SavePluginConfig(Configuration);
                Log.Information("AutoRegisterWithServerAsync: registered successfully, API key saved");
            }
            else
            {
                Log.Warning("AutoRegisterWithServerAsync: login failed — {Error}", result.Error ?? result.Value.Message ?? "unknown");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AutoRegisterWithServerAsync failed");
        }
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
        MinionCacheManager.Dispose();
        MountCacheManager.Dispose();

        // Unregister command handler
        _commandManager.RemoveHandler("/alpha");

        // Unregister UI event handlers
        _pluginInterface.UiBuilder.Draw -= ws.Draw;
        _pluginInterface.UiBuilder.OpenMainUi -= delegate { ModernMainWindow.IsOpen = true; };
        _pluginInterface.UiBuilder.OpenConfigUi -= delegate { ModernMainWindow.IsOpen = true; };

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