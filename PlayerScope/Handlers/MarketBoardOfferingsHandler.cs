using System;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Game.Network.Structures;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using PlayerScope.API.Models;
using PlayerScope.Database;

namespace PlayerScope.Handlers;

internal sealed class MarketBoardOfferingsHandler : IDisposable
{
    private readonly IMarketBoard _marketBoard;
    private readonly ILogger<MarketBoardOfferingsHandler> _logger;
    private readonly IClientState _clientState;
    private readonly PersistenceContext _persistenceContext;

    public MarketBoardOfferingsHandler(
        IMarketBoard marketBoard,
        ILogger<MarketBoardOfferingsHandler> logger,
        IClientState clientState,
        PersistenceContext persistenceContext)
    {
        _marketBoard = marketBoard;
        _logger = logger;
        _clientState = clientState;
        _persistenceContext = persistenceContext;

        _marketBoard.OfferingsReceived += HandleOfferings;
        _logger.LogInformation("MarketBoardOfferingsHandler initialized and subscribed to OfferingsReceived event");
    }

    public void Dispose()
    {
        _marketBoard.OfferingsReceived -= HandleOfferings;
    }

    private void HandleOfferings(IMarketBoardCurrentOfferings currentOfferings)
    {
        _logger.LogInformation("HandleOfferings called - Market board data received");
        
        ushort worldId = (ushort?)_clientState.LocalPlayer?.CurrentWorld.RowId ?? 0;
        if (worldId == 0)
        {
            _logger.LogInformation("Skipping market board handler, current world unknown");
            return;
        }
        
        _logger.LogInformation($"Processing market board data for world {worldId}, found {currentOfferings.ItemListings.Count} listings");
        
        // Debug each listing before filtering
        foreach (var listing in currentOfferings.ItemListings.Cast<MarketBoardCurrentOfferings.MarketBoardItemListing>())
        {
            _logger.LogInformation($"Listing: RetainerId={listing.RetainerId}, RetainerOwnerId={listing.RetainerOwnerId}, RetainerName='{listing.RetainerName}'");
        }
        
        var updates =
               currentOfferings.ItemListings
                   .Cast<MarketBoardCurrentOfferings.MarketBoardItemListing>()
                   .DistinctBy(o => o.RetainerId)
                   .Where(l => {
                       bool isValid = l.RetainerId != 0;
                       if (!isValid) _logger.LogInformation($"Filtering out listing with RetainerId=0");
                       return isValid;
                   })
                   // Note: RetainerOwnerId is often 0 in market board data, so we'll store retainers without owner info for now
                   // .Where(l => l.RetainerOwnerId != 0)
                   .Select(l =>
                       new Retainer
                       {
                           LocalContentId = l.RetainerId,
                           Name = l.RetainerName,
                           WorldId = worldId,
                           OwnerLocalContentId = l.RetainerOwnerId == 0 ? null : l.RetainerOwnerId, // Store null if owner unknown
                       }).ToList();

        _logger.LogInformation($"Found {updates.Count} valid retainers to process");
        
        foreach (var retainer in updates)
        {
            _logger.LogInformation($"Retainer: {retainer.Name} (ID: {retainer.LocalContentId}) owned by {retainer.OwnerLocalContentId}");
        }

        var toRetainerRequests = updates
            .Where(a => a.OwnerLocalContentId.HasValue) // Only upload retainers with known owners
            .Select(a => new PostRetainerRequest
            {
                LocalContentId = a.LocalContentId,
                Name = a.Name,
                WorldId = a.WorldId,
                OwnerLocalContentId = a.OwnerLocalContentId!.Value,
                CreatedAt = Tools.UnixTime,
            }).ToList();

        Task.Run(() => _persistenceContext.HandleMarketBoardPage(updates));

        if (toRetainerRequests.Count > 0)
        {
            _logger.LogInformation($"Adding {toRetainerRequests.Count} retainers to upload queue");
            PersistenceContext.AddRetainerUploadData(toRetainerRequests);
        }
        else
        {
            _logger.LogInformation("No retainer requests to upload");
        }
    }
}
