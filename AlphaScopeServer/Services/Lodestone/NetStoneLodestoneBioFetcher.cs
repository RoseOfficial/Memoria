using NetStone;

namespace AlphaScopeServer.Services.Lodestone
{
    /// <summary>
    /// Production implementation of ILodestoneBioFetcher. Uses NetStone to scrape the
    /// character profile page and returns the Bio field.
    /// </summary>
    public sealed class NetStoneLodestoneBioFetcher : ILodestoneBioFetcher
    {
        private readonly ILogger<NetStoneLodestoneBioFetcher> _logger;
        private readonly LodestoneClient _client;

        public NetStoneLodestoneBioFetcher(ILogger<NetStoneLodestoneBioFetcher> logger)
        {
            _logger = logger;
            _client = LodestoneClient.GetClientAsync().GetAwaiter().GetResult();
        }

        public async Task<BioFetchResult> FetchBioAsync(int lodestoneId, CancellationToken ct)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

                var character = await _client.GetCharacter(lodestoneId.ToString());
                if (character is null)
                    return new BioFetchResult(false, null, "character not found on Lodestone");

                return new BioFetchResult(true, character.Bio ?? string.Empty, null);
            }
            catch (OperationCanceledException)
            {
                return new BioFetchResult(false, null, "timeout");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Lodestone fetch failed for {LodestoneId}: {Message}", lodestoneId, ex.Message);
                return new BioFetchResult(false, null, "network error");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lodestone parse failure for {LodestoneId}: {Message}", lodestoneId, ex.Message);
                return new BioFetchResult(false, null, "parse failure");
            }
        }
    }
}
