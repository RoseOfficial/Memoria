using Dalamud.Extensions.MicrosoftLogging;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AlphaScope.API;
using AlphaScope.Database;
using AlphaScope.GUI;
using AlphaScope.Handlers;
using AlphaScope.Properties;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;

namespace AlphaScope;

public sealed class Plugin : IDalamudPlugin
{
    public const string DatabaseFileName = "AlphaScope.data.sqlite3";
    private readonly string _sqliteConnectionString;
    public static ServiceProvider? _serviceProvider;
    private readonly ICommandManager _commandManager;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    internal static IDataManager DataManager { get; set; } = null!;
    internal static IGameGui _gameGui { get; set; } = null!;
    [PluginService] internal static INotificationManager Notification { get; private set; } = null!;
    internal static Plugin Instance { get; private set; } = null!;
    internal static IPluginLog Log { get; private set; } = null!;
    public Configuration Configuration { get; }
    public ApiClient ApiClient { get; set; }
    public GUI.SettingsWindow ConfigWindow;
    public GUI.MainWindow MainWindow;
    public GUI.DetailsWindow DetailsWindow;
    public GUI.MainWindowTab.WorldSelectorWindow WorldSelectorWindow;
    public GUI.ClaimLodestoneWindow ClaimLodestoneWindow;
    public GUI.AvatarViewerWindow AvatarViewerWindow;

    internal WindowSystem ws;
    internal IDalamudPluginInterface _pluginInterface {  get; }
    public static AvatarCacheManager AvatarCacheManager;
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
        Instance = this;
        Log = pluginLog;
        ServiceCollection serviceCollection = new();
        serviceCollection.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace)
            .ClearProviders()
            .AddDalamudLogger(pluginLog)
            .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));
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

        serviceCollection.AddSingleton<PersistenceContext>();
        serviceCollection.AddSingleton<MarketBoardOfferingsHandler>();
        serviceCollection.AddSingleton<MarketBoardUiHandler>();
        serviceCollection.AddSingleton<CWLSHandler>();
        serviceCollection.AddSingleton<ObjectTableHandler>();
        serviceCollection.AddSingleton<GameHooks>();
        serviceCollection.AddSingleton<ApiClient>();
        
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        if (string.IsNullOrWhiteSpace(Configuration.Language.ToString()))
        {
            if (CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "tr")
            { Loc.Culture = new CultureInfo("tr"); Configuration.Language = Configuration.LanguageEnum.tr; }
            else
            { Loc.Culture = new CultureInfo("en"); Configuration.Language = Configuration.LanguageEnum.en; }
            pluginInterface.SavePluginConfig(Configuration);
        }
        else { Loc.Culture = new CultureInfo(Configuration.Language.ToString()); }

        if (Configuration.FreshInstall && string.IsNullOrWhiteSpace(Configuration.Key))
        {
            Configuration.FreshInstall = false; Configuration.Key = Utils.GenerateRandomKey();
            pluginInterface.SavePluginConfig(Configuration);
        }


        _pluginInterface = pluginInterface;
        _commandManager = commandManager;

        Handlers.ContextMenu.Enable();
        DataManager = dataManager;
        _gameGui = gameGui;

        ws = new();
        MainWindow = new();
        DetailsWindow = new();
        WorldSelectorWindow = new();
        ClaimLodestoneWindow = new();
        AvatarViewerWindow = new();
        ConfigWindow = new();
        
        ws.AddWindow(MainWindow);
        ws.AddWindow(DetailsWindow);
        ws.AddWindow(WorldSelectorWindow);
        ws.AddWindow(ClaimLodestoneWindow);
        ws.AddWindow(AvatarViewerWindow);
        ws.AddWindow(ConfigWindow);
        
        AvatarCacheManager = new AvatarCacheManager();

        pluginInterface.UiBuilder.Draw += ws.Draw;

        pluginInterface.UiBuilder.OpenMainUi += delegate { MainWindow.IsOpen = true; };
        pluginInterface.UiBuilder.OpenConfigUi += ConfigWindow.Toggle;

        _commandManager.AddHandler("/alpha", new CommandInfo(ProcessCommand)
        {
            HelpMessage = Loc.CmOpenUI
        });

        _sqliteConnectionString = PrepareSqliteDb(serviceCollection, pluginInterface.GetPluginConfigDirectory());
        _serviceProvider = serviceCollection.BuildServiceProvider();
        ApiClient = _serviceProvider.GetRequiredService<ApiClient>();

        RunMigrations(_serviceProvider);
        InitializeRequiredServices(_serviceProvider);
    }
    private void ProcessCommand(string command, string arguments)
    {
        if (command == "/alpha")
        {
            MainWindow.IsOpen = true;
        }
    }

    private static string PrepareSqliteDb(IServiceCollection serviceCollection, string getPluginConfigDirectory)
    {
        string connectionString = $"Data Source={Path.Join(getPluginConfigDirectory, DatabaseFileName)}";
        serviceCollection.AddDbContext<RetainerTrackContext>(o => o
            .UseSqlite(connectionString));
            //.UseModel(RetainerTrackContextModel.Instance));
        return connectionString;
    }

    private static void RunMigrations(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<RetainerTrackContext>();
        
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            connection.Open();
            
            using var command = connection.CreateCommand();
            
            // Check if database exists and create with correct schema if needed
            command.CommandText = @"
                SELECT name FROM sqlite_master WHERE type='table' AND name='Retainers';
            ";
            
            var result = command.ExecuteScalar();
            bool retainersTableExists = result != null;
            
            if (!retainersTableExists)
            {
                // Create the database with the correct nullable schema from scratch
                command.CommandText = @"
                    CREATE TABLE Players (
                        LocalContentId INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        AccountId INTEGER NULL,
                        CurrentJobId INTEGER NULL,
                        CurrentJobLevel INTEGER NULL
                    );
                    
                    CREATE TABLE Retainers (
                        LocalContentId INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        WorldId INTEGER NOT NULL,
                        OwnerLocalContentId INTEGER NULL
                    );
                ";
                command.ExecuteNonQuery();
            }
            else
            {
                // Check if we need to update existing schema
                command.CommandText = @"
                    PRAGMA table_info(Retainers);
                ";
                
                using var reader = command.ExecuteReader();
                bool needsUpdate = false;
                while (reader.Read())
                {
                    var columnName = reader.GetString(1); // name column
                    var notNull = reader.GetInt32(3); // notnull column
                    
                    if (columnName == "OwnerLocalContentId" && notNull == 1)
                    {
                        needsUpdate = true;
                        break;
                    }
                }
                reader.Close();
                
                if (needsUpdate)
                {
                    // Update the schema to make OwnerLocalContentId nullable
                    command.CommandText = @"
                        BEGIN TRANSACTION;
                        
                        -- Create new table with nullable OwnerLocalContentId
                        CREATE TABLE Retainers_New (
                            LocalContentId INTEGER PRIMARY KEY,
                            Name TEXT NOT NULL,
                            WorldId INTEGER NOT NULL,
                            OwnerLocalContentId INTEGER NULL
                        );
                        
                        -- Copy data from old table
                        INSERT INTO Retainers_New (LocalContentId, Name, WorldId, OwnerLocalContentId)
                        SELECT LocalContentId, Name, WorldId, 
                               CASE WHEN OwnerLocalContentId = 0 THEN NULL ELSE OwnerLocalContentId END
                        FROM Retainers;
                        
                        -- Drop old table and rename new one
                        DROP TABLE Retainers;
                        ALTER TABLE Retainers_New RENAME TO Retainers;
                        
                        COMMIT;
                    ";
                    
                    command.ExecuteNonQuery();
                }
                
                // Check if Players table needs job data columns
                command.CommandText = @"
                    PRAGMA table_info(Players);
                ";
                
                using var playerReader = command.ExecuteReader();
                bool hasJobId = false;
                bool hasJobLevel = false;
                while (playerReader.Read())
                {
                    var columnName = playerReader.GetString(1); // name column
                    if (columnName == "CurrentJobId") hasJobId = true;
                    if (columnName == "CurrentJobLevel") hasJobLevel = true;
                }
                playerReader.Close();
                
                // Add job columns if they don't exist
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
            }
            
            connection.Close();
        }
        catch (Exception ex)
        {
            // Log error but don't crash the plugin
            System.Console.WriteLine($"Error setting up database: {ex.Message}");
            throw; // Re-throw since we need the database to work
        }
    }

    private static void InitializeRequiredServices(ServiceProvider serviceProvider)
    {
        serviceProvider.GetRequiredService<MarketBoardOfferingsHandler>();
        serviceProvider.GetRequiredService<MarketBoardUiHandler>();
        serviceProvider.GetRequiredService<CWLSHandler>();
        serviceProvider.GetRequiredService<ObjectTableHandler>();
        serviceProvider.GetRequiredService<GameHooks>();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        Handlers.ContextMenu.Disable();
        PersistenceContext.StopUploads();
        AvatarCacheManager.Dispose();

        _pluginInterface.UiBuilder.Draw -= ws.Draw;
        _pluginInterface.UiBuilder.OpenMainUi -= delegate { MainWindow.IsOpen = true; };
        _pluginInterface.UiBuilder.OpenConfigUi -= ConfigWindow.Toggle;

        // ensure we're not keeping the file open longer than the plugin is loaded
        using (SqliteConnection sqliteConnection = new(_sqliteConnectionString))
            SqliteConnection.ClearPool(sqliteConnection);
    }
}