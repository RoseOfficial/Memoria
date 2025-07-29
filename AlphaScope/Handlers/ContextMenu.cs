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
                    Name = "See Detailed Info",
                    OnClicked = SearchDetailedPlayerInfoById
                });
            }
            else if (!string.IsNullOrEmpty(menuTargetDefault.TargetName))
            {
                menuOpenedArgs.AddMenuItem(new MenuItem
                {
                    PrefixColor = 15,
                    PrefixChar = 'P',
                    Name = "Search Player By Name",
                    OnClicked = SearchPlayerName
                });
            }
        }
    }

    private static void SearchDetailedPlayerInfoById(IMenuItemClickedArgs menuArgs)
    {
        if (menuArgs.Target is not MenuTargetDefault menuTargetDefault)
        {
            return;
        }
        ulong? targetCId = menuTargetDefault.TargetContentId;

        // Open modern UI instead
        Plugin.Instance.ModernMainWindow.IsOpen = true;
        Plugin.Log.Info($"Opening details for player {targetCId} - Details view coming soon in modern UI");
    }

    private static void SearchPlayerName(IMenuItemClickedArgs menuArgs)
    {
        if (menuArgs.Target is not MenuTargetDefault menuTargetDefault)
        {
            return;
        }

        var targetName = string.Empty;

        if (menuArgs.AddonName == "BlackList")
        {
            targetName = GetBlacklistSelectPlayerName();
        }
        else if (menuArgs.AddonName == "MuteList")
        {
            targetName = GetMuteListSelectFullName();
        }
        else
        {
            targetName = menuTargetDefault.TargetName;
        }

        // Open modern UI for search
        Plugin.Instance.ModernMainWindow.IsOpen = true;
        Plugin.Log.Info($"Searching for player {targetName} - Search functionality coming soon in modern UI");
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