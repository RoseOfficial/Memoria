using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Lumina.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Memoria.API.Models.Responses.Player;
using Memoria.API.Models.Responses.User;
using Memoria.API.Models.Responses.Server;
using Memoria.API.Models.Responses.Common;
using Memoria.API.Models.Requests.Player;
using Memoria.GUI;
using Memoria.Models;

namespace Memoria.Handlers;

internal sealed class ObjectTableHandler : IDisposable
{
    private readonly IObjectTable _objectTable;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly ILogger<ObjectTableHandler> _logger;
    private readonly PersistenceContext _persistenceContext;

    private long _lastUpdate;

    public ObjectTableHandler(IObjectTable objectTable, IFramework framework, IClientState clientState, ILogger<ObjectTableHandler> logger, PersistenceContext persistenceContext)
    {
        _objectTable = objectTable;
        _framework = framework;
        _clientState = clientState;
        _logger = logger;
        _persistenceContext = persistenceContext;

        _framework.Update += FrameworkUpdate;
    }

    private unsafe void FrameworkUpdate(IFramework framework)
    {
        long now = Environment.TickCount64;
        if (!_clientState.IsLoggedIn || now - _lastUpdate < Plugin.Instance.Configuration.ObjectTableRefreshInterval)
            return;

        _lastUpdate = now;

        if (Process.GetProcessesByName("Anamnesis").Length != 0)
            PersistenceContext.AnamnesisFound = true;

        // Resolve TerritoryName once per scan via the known-working Tools helper —
        // empirically the in-line destructure pattern was returning null in production
        // (TerritoryNames table never populated), and Tools.GetTerritoryName is the
        // form already proven against the bundled Lumina version. Strip the region
        // suffix ("Limsa Lominsa Upper Decks, La Noscea" → "Limsa Lominsa Upper Decks")
        // since the Locations panel only needs the place name.
        var localTerritoryId = (short)PersistenceContext._clientState.TerritoryType;
        string? localTerritoryName = null;
        try
        {
            var combined = Tools.GetTerritoryName((ushort)localTerritoryId);
            if (!string.IsNullOrWhiteSpace(combined) && combined != "[Unknown]")
            {
                var commaIdx = combined.IndexOf(',');
                localTerritoryName = commaIdx > 0 ? combined.Substring(0, commaIdx).Trim() : combined;
            }
        }
        catch
        {
            // Lumina lookup failures are non-fatal — server falls back to "Territory N".
        }

        // Match AlphaScope's original behavior: read bc->AccountId for every player.
        // The field is unreliable on its own (it sometimes returns the local user's
        // value when the spawn packet hasn't fully landed), but the new spawn-packet
        // hook in GameHooks lands first with verified data and the server's
        // first-scan-wins logic locks that in. Object-table reads are the fallback
        // for players already in the zone when we joined.
        List<PlayerMapping> playerMappings = new();
        List<PostPlayerRequest> playerRequests = new();
        foreach (var obj in _objectTable)
        {
            if (obj.ObjectKind == ObjectKind.Player)
            {
                var bc = (Character*)obj.Address;
                if (bc->ContentId == 0 || bc->AccountId == 0)
                    continue;

                playerMappings.Add(new PlayerMapping
                {
                    ContentId = bc->ContentId,
                    AccountId = bc->AccountId,
                    PlayerName = bc->NameString,
                    WorldId = bc->HomeWorld,
                    CurrentWorldId = bc->CurrentWorld,
                });

                var Customization = bc->DrawData.CustomizeData;

                // Get world IDs directly from game data
                var homeWorld = bc->HomeWorld;
                var currentWorld = bc->CurrentWorld;

                playerRequests.Add(new PostPlayerRequest
                {
                    LocalContentId = bc->ContentId,
                    Name = bc->NameString,
                    AccountId = (int?)bc->AccountId,
                    HomeWorldId = homeWorld,
                    CurrentWorldId = currentWorld,
                    TerritoryId = localTerritoryId,
                    TerritoryName = localTerritoryName,
                    CurrentJobId = bc->CharacterData.ClassJob != 0 ? bc->CharacterData.ClassJob : null,
                    CurrentJobLevel = bc->CharacterData.Level != 0 ? (short)bc->CharacterData.Level : null,
                    PlayerPos = Utils.Vector3ToString(obj.GetMapCoordinates()),
                    Customization = PersistenceContext.AnamnesisFound ? null : new PlayerCustomization
                    {
                        BodyType = Customization.BodyType,
                        BustSize = Customization.BustSize,
                        EyeShape = Customization.EyeShape,
                        Face = Customization.Face,
                        Height = Customization.Height,
                        Jaw = Customization.Jaw,
                        Mouth = Customization.Mouth,
                        MuscleMass = Customization.MuscleMass,
                        Nose = Customization.Nose,
                        SkinColor = Customization.SkinColor,
                        SmallIris = Customization.SmallIris,
                        TailShape = Customization.TailShape,
                        GenderRace = ((byte)Models.RaceEnumExtensions.CombinedRace((Gender)bc->DrawData.CustomizeData.Sex, (SubRace)bc->DrawData.CustomizeData.Tribe))
                    },
                    CreatedAt = Tools.UnixTime,
                });
            }
        }

        if (playerMappings.Count > 0)
        {
            var currentWorldId = (ushort?)PersistenceContext.GetCurrentWorld();
            Task.Run(() => _persistenceContext.HandleContentIdMappingAsync(playerMappings, currentWorldId));
            
            // Only queue truly NEW players for Lodestone refresh (not every player every frame)
            foreach (var mapping in playerMappings)
            {
                // Check if this is a newly discovered player (not in cache)
                if (!PersistenceContext.IsPlayerCached(mapping.ContentId))
                {
                    PersistenceContext.QueuePlayerForLodestoneRefresh(mapping.ContentId, isNewPlayer: true);
                }
            }
        }

        // Queue players for server upload
        if (playerRequests.Count > 0)
            PersistenceContext.AddPlayerUploadData(playerRequests);
    #if DEBUG
        _logger.LogTrace("ObjectTable handling for {Count} players took {TimeMs}", playerMappings.Count, TimeSpan.FromMilliseconds(Environment.TickCount64 - now));
    #endif
    }

    public void Dispose()
    {
        _framework.Update -= FrameworkUpdate;
    }
}