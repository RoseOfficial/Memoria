using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Microsoft.Extensions.Logging;
using AlphaScope.API.Models.Responses.Player;
using AlphaScope.API.Models.Responses.User;
using AlphaScope.API.Models.Responses.Server;
using AlphaScope.API.Models.Responses.Common;
using AlphaScope.API.Query;
using AlphaScope.GUI;
using System.Linq;

namespace AlphaScope.Handlers;

public class ContextMenu
{
    public static void Enable()
    {
        Plugin.ContextMenu.OnMenuOpened -= OnOpenContextMenu;
        Plugin.ContextMenu.OnMenuOpened += OnOpenContextMenu;
    }

    public static void Disable()
    {
        Plugin.ContextMenu.OnMenuOpened -= OnOpenContextMenu;
    }

    private static bool IsMenuValid(IMenuArgs menuOpenedArgs)
    {
        if (menuOpenedArgs.Target is not MenuTargetDefault menuTargetDefault)
        {
            return false;
        }

        switch (menuOpenedArgs.AddonName)
        {
            case null: // Nameplate/Model menu
            case "CircleBook": // Fellowships
            case "LookingForGroup":
            case "PartyMemberList":
            case "FriendList":
            case "FreeCompany":
            case "SocialList":
            case "ContactList":
            case "ChatLog":
            case "_PartyList":
            case "LinkShell":
            case "CrossWorldLinkshell":
            case "ContentMemberList": // Eureka/Bozja/...
            case "BeginnerChatList":
                return menuTargetDefault.TargetName != string.Empty && Utils.IsWorldValid(menuTargetDefault.TargetHomeWorld.RowId);
            case "BlackList":
            case "MuteList":
                return menuTargetDefault.TargetName != string.Empty;
        }

        return false;
    }

    private static void OnOpenContextMenu(IMenuOpenedArgs menuOpenedArgs)
    {
        if (menuOpenedArgs.Target is not MenuTargetDefault menuTargetDefault)
        {
            return;
        }
        
        if (!IsMenuValid(menuOpenedArgs))
            return;
        
        if (menuTargetDefault.TargetHomeWorld.RowId < 10000)
        {
            if (menuTargetDefault.TargetContentId != 0)
            {
                menuOpenedArgs.AddMenuItem(new MenuItem
                {
                    PrefixColor = 15,
                    PrefixChar = 'P',
                    Name = "View in AlphaScope",
                    OnClicked = SearchDetailedPlayerInfoById
                });
            }
            else if (!string.IsNullOrEmpty(menuTargetDefault.TargetName))
            {
                menuOpenedArgs.AddMenuItem(new MenuItem
                {
                    PrefixColor = 15,
                    PrefixChar = 'P',
                    Name = "View in AlphaScope",
                    OnClicked = SearchPlayerName
                });
            }
        }
    }

    private static void SearchDetailedPlayerInfoById(IMenuItemClickedArgs menuArgs)
    {
        if (menuArgs.Target is not MenuTargetDefault menuTargetDefault) return;
        var targetCId = menuTargetDefault.TargetContentId;
        if (targetCId == 0) return;

        if (Plugin.Instance.Configuration.OptOutInGamePopups)
        {
            Plugin.Instance.MainWindow.OpenAtSearch(menuTargetDefault.TargetName);
            return;
        }

        Plugin.Instance.MicroCard.OpenFor(targetCId);
    }

    private static void SearchPlayerName(IMenuItemClickedArgs menuArgs)
    {
        if (menuArgs.Target is not MenuTargetDefault menuTargetDefault) return;

        var targetName = menuArgs.AddonName switch
        {
            "BlackList" => GetBlacklistSelectPlayerName(),
            "MuteList"  => GetMuteListSelectFullName(),
            _           => menuTargetDefault.TargetName,
        };

        Plugin.Instance.MainWindow.OpenAtSearch(targetName);
    }

    private static unsafe string GetBlacklistSelectPlayerName()
    {
        var agentBlackList = (AgentBlacklist*)Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Blacklist);
        if (agentBlackList != null)
        {
            return MemoryHelper.ReadSeString(&agentBlackList->SelectedPlayerName).TextValue;
        }

        return string.Empty;
    }

    private static unsafe string GetMuteListSelectFullName()
    {
        var agentMuteList = Framework.Instance()->GetUIModule()->GetAgentModule()->GetAgentByInternalId(AgentId.Mutelist);
        if (agentMuteList != null)
        {
            return MemoryHelper.ReadSeStringNullTerminated(*(nint*)((nint)agentMuteList + 0x58)).TextValue; // should create the agent in CS later
        }

        return string.Empty;
    }
}