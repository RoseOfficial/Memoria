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

        // Resolve TerritoryName once per scan via the safest Lumina API path:
        // GetRowOrDefault (no exceptions for missing ids) + ValueNullable on the
        // RowRef so an unresolvable PlaceName doesn't throw either. Two earlier
        // attempts (the inline destructure pattern, and the Tools.GetTerritoryName
        // helper that uses ExcelResolver+GetRow) both produced an empty
        // TerritoryNames table in production despite many uploads — this version
        // logs each step at Information so we can see exactly which link in the
        // chain is failing if it still doesn't populate.
        var localTerritoryId = (short)PersistenceContext._clientState.TerritoryType;
        string? localTerritoryName = null;
        try
        {
            var sheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
            if (sheet is null)
            {
                _logger.LogWarning("[Territory] GetExcelSheet<TerritoryType>() returned null");
            }
            else
            {
                var row = sheet.GetRowOrDefault((uint)localTerritoryId);
                if (!row.HasValue)
                {
                    _logger.LogInformation("[Territory] row {Id} not found in sheet", localTerritoryId);
                }
                else
                {
                    var placeName = row.Value.PlaceName.ValueNullable;
                    if (!placeName.HasValue)
                    {
                        _logger.LogInformation("[Territory] PlaceName.ValueNullable is null for territory {Id}", localTerritoryId);
                    }
                    else
                    {
                        var name = placeName.Value.Name.ToString();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            _logger.LogInformation("[Territory] PlaceName.Name resolved to empty for territory {Id}", localTerritoryId);
                        }
                        else
                        {
                            localTerritoryName = name;
                            _logger.LogInformation("[Territory] {Id} resolved to '{Name}'", localTerritoryId, name);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Territory] Lookup threw for territory {Id}", localTerritoryId);
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
                    AccountId = unchecked((long)bc->AccountId),
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
                    // Phase 1 — Character struct + nested containers. All "0 means
                    // not set" filters: TitleId 0 = no title, OnlineStatus 0 = no
                    // explicit status, MountId 0 = not mounted, CompanionId 0 = no
                    // minion summoned. FC tag is read from offset 0x2300; an empty
                    // first byte means no FC.
                    OnlineStatusId = bc->CharacterData.OnlineStatus != 0 ? bc->CharacterData.OnlineStatus : null,
                    TitleId = bc->CharacterData.TitleId != 0 ? (int?)bc->CharacterData.TitleId : null,
                    CurrentMountId = bc->Mount.MountId != 0 ? (int?)bc->Mount.MountId : null,
                    CurrentMinionId = bc->CompanionData.CompanionId != 0 ? (int?)bc->CompanionData.CompanionId : null,
                    FreeCompanyTag = ReadFreeCompanyTag(bc),
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

    private static unsafe string? ReadFreeCompanyTag(Character* bc)
    {
        // _freeCompanyTag is FixedSizeArray7<byte> at offset 0x2300, declared with
        // isString: true. The InteropGenerator's exposed accessor varies between
        // versions, so we read directly via MemoryHelper for portability. Empty
        // first byte means the player isn't in an FC.
        var tag = MemoryHelper.ReadString((nint)bc + 0x2300, Encoding.UTF8, 7);
        return string.IsNullOrEmpty(tag) ? null : tag;
    }

    public void Dispose()
    {
        _framework.Update -= FrameworkUpdate;
    }
}