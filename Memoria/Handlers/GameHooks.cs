using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Hooking;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Microsoft.Extensions.Logging;
using Memoria.API.Models.Responses.Player;
using Memoria.API.Models.Responses.User;
using Memoria.API.Models.Responses.Server;
using Memoria.API.Models.Responses.Common;
using Memoria.API.Models.Requests.Player;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCommonList.CharacterData;

namespace Memoria.Handlers;

internal sealed unsafe class GameHooks : IDisposable
{
    private readonly ILogger<GameHooks> _logger;
    private readonly PersistenceContext _persistenceContext;

    /// <summary>
    /// Processes the content id to character name packet, seen e.g. when you hover an item to retrieve the
    /// crafter's signature.
    /// </summary>
    private delegate int CharacterNameResultDelegate(nint a1, ulong contentId, char* playerName);

    private delegate nint SocialListResultDelegate(nint a1, nint dataPtr);

    /// <summary>
    /// Hooks PacketDispatcher.HandleSpawnPlayerPacket — fires every time another player
    /// enters our visibility range. The packet carries server-verified AccountId at
    /// offset 0, ContentId at 0x08, world ids at 0x14/0x16, and the player name embedded
    /// in CommonSpawnData. This is the path the Character struct's bc-&gt;AccountId field
    /// can't be trusted to provide — the game writes that slot reliably only for the
    /// local user's character, so spawn-packet capture is how we get accurate per-player
    /// AccountIds for everyone we walk past.
    /// </summary>
    private delegate void HandleSpawnPlayerPacketDelegate(uint targetId, SpawnPlayerPacket* packet);

#pragma warning disable CS0649
    [Signature("40 53 48 83 EC 20 48 8B  D9 33 C9 45 33 C9", DetourName = nameof(ProcessCharacterNameResult))]
    private Hook<CharacterNameResultDelegate> CharacterNameResultHook { get; init; } = null!;

    // Signature adapted from https://github.com/LittleNightmare/UsedName
    [Signature("48 89 5C 24 10 56 48 83 EC 20 48 ?? ?? ?? ?? ?? ?? 48 8B F2 E8 ?? ?? ?? ?? 48 8B D8",
        DetourName = nameof(ProcessSocialListResult))]
    private Hook<SocialListResultDelegate> SocialListResultHook { get; init; } = null!;

    // Signature from FFXIVClientStructs PacketDispatcher.HandleSpawnPlayerPacket.
    [Signature("48 89 5C 24 ?? 57 48 81 EC ?? ?? ?? ?? 48 8B DA 8B F9 0F B6 92",
        DetourName = nameof(ProcessSpawnPlayerPacket))]
    private Hook<HandleSpawnPlayerPacketDelegate> SpawnPlayerPacketHook { get; init; } = null!;

#pragma warning restore CS0649

    public GameHooks(ILogger<GameHooks> logger, PersistenceContext persistenceContext,
        IGameInteropProvider gameInteropProvider)
    {
        _logger = logger;
        _persistenceContext = persistenceContext;

        gameInteropProvider.InitializeFromAttributes(this);
        CharacterNameResultHook.Enable();
        SocialListResultHook.Enable();
        SpawnPlayerPacketHook.Enable();
    }

    private int ProcessCharacterNameResult(nint a1, ulong contentId, char* playerName)
    {
        try
        {
            var mapping = new PlayerMapping
            {
                ContentId = contentId,
                AccountId = null,
                PlayerName = MemoryHelper.ReadString(new nint(playerName), Encoding.ASCII, 32),
            };

            if (!string.IsNullOrEmpty(mapping.PlayerName))
            {
                _logger.LogTrace("Content id {ContentId} belongs to '{Name}'", mapping.ContentId,
                    mapping.PlayerName);
                if (mapping.PlayerName.IsValidCharacterName(true))
                {
                    var playerRequest = new PostPlayerRequest
                    {
                        LocalContentId = contentId,
                        Name = mapping.PlayerName,
                        AccountId = mapping.AccountId.HasValue ? unchecked((long)mapping.AccountId.Value) : (long?)null,
                        HomeWorldId = null,
                        CurrentWorldId = (ushort?)PersistenceContext.GetCurrentWorld(),
                        CreatedAt = Tools.UnixTime,
                    };

                    var currentWorldId = (ushort?)playerRequest.CurrentWorldId;
                    Task.Run(() => _persistenceContext.HandleContentIdMappingAsync(new List<PlayerMapping> { mapping }, currentWorldId));
                    PersistenceContext.AddPlayerUploadData(new List<PostPlayerRequest> { playerRequest });
                }
            }
            else
            {
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not process character name result");
        }

        return CharacterNameResultHook.Original(a1, contentId, playerName);
    }

    private nint ProcessSocialListResult(nint a1, nint dataPtr)
    {
        try
        {
            var result = Marshal.PtrToStructure<SocialListResultPage>(dataPtr);
            List<PlayerMapping> mappings = new();
            List<PostPlayerRequest> playerRequests = new();
            foreach (SocialListPlayer player in result.PlayerSpan)
            {
                if (player.ContentId == 0)
                    continue;

                ushort? homeWorldId = player.HomeWorldID != 0 && player.HomeWorldID != 65535 ? player.HomeWorldID : null;

                var mapping = new PlayerMapping
                {
                    ContentId = player.ContentId,
                    AccountId = player.AccountId != 0 ? player.AccountId : null,
                    PlayerName = MemoryHelper.ReadString(new nint(player.CharacterName), Encoding.ASCII, 32),
                    WorldId = homeWorldId,
                };

                if (!string.IsNullOrEmpty(mapping.PlayerName))
                {
                    mappings.Add(mapping);
                    playerRequests.Add(new PostPlayerRequest
                    {
                        LocalContentId = mapping.ContentId,
                        Name = mapping.PlayerName,
                        AccountId = mapping.AccountId.HasValue ? unchecked((long)mapping.AccountId.Value) : (long?)null,
                        HomeWorldId = mapping.WorldId,
                        CurrentWorldId = (ushort?)PersistenceContext.GetCurrentWorld(),
                        TerritoryId = player.TerritoryId == 0 ? (short?)null : (short)player.TerritoryId,
                        CurrentJobId = player.CurrentJobId != 0 ? player.CurrentJobId : null,
                        CurrentJobLevel = player.CurrentJobLevel != 0 ? (short)player.CurrentJobLevel : null,
                        // Phase 1 — SocialListPlayer-specific reads. GC and FC tag
                        // are highly reliable from this path (party rosters, FC member
                        // lists). OnlineStatus is encoded as a bitmask in the ulong;
                        // grab the first byte as the primary status.
                        GrandCompanyId = (byte)player.GrandCompanyId != 0 ? (byte)player.GrandCompanyId : null,
                        OnlineStatusId = (byte)(player.OnlineStatusBytes & 0xFF) != 0
                            ? (byte)(player.OnlineStatusBytes & 0xFF) : null,
                        FreeCompanyTag = player.GetFreeCompanyTag(),
                        CreatedAt = Tools.UnixTime,
                    });
                }
                else
                {
                    //_logger.LogDebug("Content id {ContentId} didn't resolve to a player name, ignoring", mapping.ContentId);
                }
            }

            if (mappings.Count > 0)
            {
                var currentWorldId = (ushort?)PersistenceContext.GetCurrentWorld();
                Task.Run(() => _persistenceContext.HandleContentIdMappingAsync(mappings, currentWorldId));
            }
            
            if (playerRequests.Count > 0)
                PersistenceContext.AddPlayerUploadData(playerRequests);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not process social list result");
        }

        return SocialListResultHook.Original(a1, dataPtr);
    }

    private void ProcessSpawnPlayerPacket(uint targetId, SpawnPlayerPacket* packet)
    {
        try
        {
            if (packet == null)
            {
                SpawnPlayerPacketHook.Original(targetId, packet);
                return;
            }

            var contentId = packet->ContentId;
            var accountId = packet->AccountId;

            // Skip if either id is missing — the packet sometimes arrives partially
            // populated for transitional spawn states (housing previews, GM-spawned
            // mannequins, etc.) and we don't want to record those as real players.
            if (contentId == 0 || accountId == 0)
            {
                _logger.LogTrace("SpawnPlayerPacket targetId={TargetId} contentId={ContentId} accountId={AccountId} — skipped (zero id)",
                    targetId, contentId, accountId);
                SpawnPlayerPacketHook.Original(targetId, packet);
                return;
            }

            _logger.LogInformation("SpawnPlayerPacket received: targetId={TargetId} contentId={ContentId} accountId={AccountId}",
                targetId, contentId, accountId);

            // Name is in CommonSpawnData._name at packet offset 0x252 (CommonSpawnData
            // starts at 0x20 within the packet, and _name is at 0x232 inside CommonSpawnData).
            // 32-byte ASCII null-terminated.
            var namePtr = (nint)packet + 0x252;
            var name = MemoryHelper.ReadString(namePtr, Encoding.ASCII, 32);
            if (string.IsNullOrEmpty(name))
            {
                SpawnPlayerPacketHook.Original(targetId, packet);
                return;
            }

            ushort? homeWorldId = packet->HomeWorldId != 0 ? packet->HomeWorldId : null;
            ushort? currentWorldId = packet->CurrentWorldId != 0 ? packet->CurrentWorldId : null;

            var mapping = new PlayerMapping
            {
                ContentId = contentId,
                AccountId = accountId,
                PlayerName = name,
                WorldId = homeWorldId,
                CurrentWorldId = currentWorldId,
            };

            var playerRequest = new PostPlayerRequest
            {
                LocalContentId = contentId,
                Name = name,
                AccountId = unchecked((long)accountId),
                HomeWorldId = homeWorldId,
                CurrentWorldId = currentWorldId,
                TerritoryId = (short)PersistenceContext._clientState.TerritoryType,
                CurrentJobId = packet->Common.ClassJobId != 0 ? packet->Common.ClassJobId : null,
                CurrentJobLevel = packet->Common.Level != 0 ? (short)packet->Common.Level : null,
                CreatedAt = Tools.UnixTime,
            };

            var currentWorldForMapping = (ushort?)PersistenceContext.GetCurrentWorld();
            Task.Run(() => _persistenceContext.HandleContentIdMappingAsync(
                new List<PlayerMapping> { mapping }, currentWorldForMapping));
            PersistenceContext.AddPlayerUploadData(new List<PostPlayerRequest> { playerRequest });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not process spawn player packet");
        }

        SpawnPlayerPacketHook.Original(targetId, packet);
    }

    public void Dispose()
    {
        CharacterNameResultHook.Dispose();
        SocialListResultHook.Dispose();
        SpawnPlayerPacketHook.Dispose();
    }

    /// <summary>
    /// There are some caveats here, the social list includes a LOT of things with different types
    /// (we don't care for the result type in this plugin), see sapphire for which field is the type.
    ///
    /// 1 = party
    /// 2 = friend list
    /// 3 = link shell
    /// 4 = player search
    /// 5 = fc short list (first tab, with company board + actions + online members)
    /// 6 = fc long list (members tab)
    ///
    /// Both 1 and 2 are sent to you on login, unprompted.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 0x420)]
    internal struct SocialListResultPage
    {
        [FieldOffset(0x10)] private fixed byte Players[10 * 0x70];

        public Span<SocialListPlayer> PlayerSpan => new(Unsafe.AsPointer(ref Players[0]), 10);
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x70, Pack = 1)]
    internal struct SocialListPlayer
    {
        /// <summary>
        /// If this is set, it means there is a player present in this slot (even if no name can be retrieved),
        /// 0 if empty.
        /// </summary>
        [FieldOffset(0x00)] public readonly ulong ContentId;

        /// <summary>
        /// Only seems to be set for certain kind of social lists, e.g. friend list/FC members doesn't include any.
        /// </summary>
        [FieldOffset(0x18)] public readonly ulong AccountId;

        [FieldOffset(0x24)] public ushort TerritoryId;
        [FieldOffset(0x28)] public GrandCompany GrandCompanyId;
        [FieldOffset(0x29)] public Language ClientLanguage;
        [FieldOffset(0x2A)] public LanguageMask Languages;
        [FieldOffset(0x2B)] public byte HasSearchComment;
        [FieldOffset(0x30)] public ulong OnlineStatusBytes;
        [FieldOffset(0x38)] public byte CurrentJobId;
        [FieldOffset(0x3A)] public ushort CurrentJobLevel;
        [FieldOffset(0x42)] public ushort HomeWorldID;

        /// <summary>
        /// This *can* be empty, e.g. if you're querying your friend list, the names are ONLY set for characters on the same world.
        /// </summary>
        [FieldOffset(0x44)] public fixed byte CharacterName[32];
        // Public so the handler can read the bytes. Layout is 7 ASCII chars
        // padded with 0x00; trim trailing nulls when converting.
        [FieldOffset(0x64)] public fixed byte FcTagBytes[7];

        public unsafe string? GetFreeCompanyTag()
        {
            fixed (byte* p = FcTagBytes)
            {
                var tag = System.Text.Encoding.UTF8.GetString(p, 7).TrimEnd('\0');
                return string.IsNullOrEmpty(tag) ? null : tag;
            }
        }
    }
}
