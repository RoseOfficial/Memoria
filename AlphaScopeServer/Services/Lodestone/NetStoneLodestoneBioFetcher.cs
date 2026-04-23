using NetStone;

namespace AlphaScopeServer.Services.Lodestone
{
    /// <summary>
    /// Production implementation of ILodestoneBioFetcher. Uses NetStone to scrape the
    /// character profile page and returns the Bio field. The NetStone client is initialized
    /// lazily on first use to avoid blocking the DI container at startup.
    /// </summary>
    public sealed class NetStoneLodestoneBioFetcher : ILodestoneBioFetcher
    {
        private readonly ILogger<NetStoneLodestoneBioFetcher> _logger;
        private LodestoneClient? _client;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public NetStoneLodestoneBioFetcher(ILogger<NetStoneLodestoneBioFetcher> logger)
        {
            _logger = logger;
        }

        private async Task<LodestoneClient> GetClientAsync()
        {
            if (_client is not null) return _client;
            await _initLock.WaitAsync();
            try
            {
                _client ??= await LodestoneClient.GetClientAsync();
                return _client;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<BioFetchResult> FetchBioAsync(int lodestoneId, CancellationToken ct)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

                var client = await GetClientAsync();
                var character = await client.GetCharacter(lodestoneId.ToString())
                    .WaitAsync(timeoutCts.Token);

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
