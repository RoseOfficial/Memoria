using System.Collections.Concurrent;
using System.Text.Json;
using MemoriaServer.Data;
using MemoriaServer.Models.Entities;
using MemoriaServer.Services.World;
using Microsoft.EntityFrameworkCore;
using NetStone;
using NetStone.Search.Character;

namespace MemoriaServer.Services.Lodestone
{
    /// <summary>
    /// Centralized server-side Lodestone enrichment. Maintains a single in-process queue of
    /// player ContentIds and processes them one at a time at a fixed cadence, fetching avatar,
    /// jobs, minions, and mounts via NetStone and writing the results back to the Player and
    /// PlayerLodestone tables.
    ///
    /// This replaces the per-user plugin-side LodestoneRefreshService for the data the server
    /// owns. The plugin path stranded enrichment in client RAM whenever a user encountered a
    /// player only briefly: ObjectTableHandler queued the player for refresh, NetStone fetched
    /// the data, but the client's HasDataChanged short-circuit (Name/World/Territory only)
    /// suppressed the next upload, so the cached jobs/minions/mounts never reached the server.
    /// Centralizing on the server guarantees every player on Lodestone gets enriched regardless
    /// of which user scanned them or how long their plugin stayed open.
    /// </summary>
    public sealed class LodestoneEnrichmentService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LodestoneEnrichmentService> _logger;
        private readonly ConcurrentQueue<long> _queue = new();
        private readonly ConcurrentDictionary<long, byte> _enqueued = new();
        private LodestoneClient? _client;
        private readonly SemaphoreSlim _clientInit = new(1, 1);

        // 2 seconds between Lodestone fetches keeps the request rate low enough to coexist
        // with other scrapers and avoids tripping rate limits even during a backlog drain.
        // Each player produces ~4 HTTP requests via NetStone (search, profile, jobs, minions,
        // mounts), so the actual outbound rate is ~2 req/s averaged over the cycle.
        private static readonly TimeSpan PerPlayerDelay = TimeSpan.FromSeconds(2);

        // When the queue empties we wait a bit longer before re-checking. New uploads keep
        // pushing players in via Enqueue, so this idle delay only matters if the queue truly
        // drained.
        private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(30);

        // Re-enrich players whose Lodestone data is older than this. Keeps job levels, mount
        // counts, and avatar fresh without hammering Lodestone for unchanged data.
        private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

        // How often to re-run the staleness seed query. The processing loop only catches
        // staleness at startup otherwise — on a long-running instance (no Render sleep, no
        // redeploy) that means rows that cross the 7-day mark mid-uptime never get re-queued.
        // Hourly is dirt-cheap (a single indexed Postgres query) and gives us a worst-case
        // 7d + 1h freshness guarantee instead of "whenever the server happens to restart."
        private static readonly TimeSpan ReseedInterval = TimeSpan.FromHours(1);

        public LodestoneEnrichmentService(
            IServiceScopeFactory scopeFactory,
            ILogger<LodestoneEnrichmentService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Adds a player to the enrichment queue. Safe to call multiple times for the same
        /// id; the second call is a no-op until the player is dequeued. Called from
        /// PlayersController.UploadPlayers after each batch save.
        /// </summary>
        public void Enqueue(long localContentId)
        {
            if (localContentId == 0) return;
            if (_enqueued.TryAdd(localContentId, 0))
                _queue.Enqueue(localContentId);
        }

        public int QueueDepth => _queue.Count;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Seed the queue from existing rows that are missing or stale, then re-seed every
            // ReseedInterval (1h) so the 7-day staleness rule keeps holding even on long-running
            // instances that never hit a redeploy or sleep. The first iteration runs immediately
            // because lastSeedAt starts at MinValue.
            var lastSeedAt = DateTime.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (DateTime.UtcNow - lastSeedAt >= ReseedInterval)
                    {
                        try
                        {
                            await SeedFromDatabaseAsync(stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "LodestoneEnrichment: re-seed failed (will try again next interval)");
                        }
                        // Stamp regardless of success so a persistent failure doesn't busy-loop
                        // the seed query — next attempt is one ReseedInterval out.
                        lastSeedAt = DateTime.UtcNow;
                    }

                    if (_queue.TryDequeue(out var localContentId))
                    {
                        _enqueued.TryRemove(localContentId, out _);
                        await ProcessPlayerAsync(localContentId, stoppingToken);
                        await Task.Delay(PerPlayerDelay, stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(IdleDelay, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LodestoneEnrichment: unexpected error in processing loop");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task SeedFromDatabaseAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();

            var staleCutoff = DateTime.UtcNow - StaleAfter;
            var ids = await db.Players
                .Where(p => p.LodestoneJobData == null
                            || p.LastJobDataUpdate == null
                            || p.LastJobDataUpdate < staleCutoff)
                .OrderBy(p => p.LastJobDataUpdate ?? DateTime.MinValue)
                .Select(p => p.LocalContentId)
                .ToListAsync(ct);

            foreach (var id in ids)
                Enqueue(id);

            _logger.LogInformation(
                "LodestoneEnrichment: seeded {Count} players for enrichment (queue depth {Depth})",
                ids.Count, _queue.Count);
        }

        private async Task ProcessPlayerAsync(long localContentId, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MemoriaDbContext>();

            var player = await db.Players.FirstOrDefaultAsync(p => p.LocalContentId == localContentId, ct);
            if (player is null)
            {
                _logger.LogDebug("LodestoneEnrichment: player {Id} no longer exists; skipping", localContentId);
                return;
            }

            if (string.IsNullOrWhiteSpace(player.Name))
            {
                _logger.LogDebug("LodestoneEnrichment: player {Id} has no name; skipping", localContentId);
                return;
            }

            FetchOutcome? outcome;
            try
            {
                outcome = await FetchAsync(player.Name, player.HomeWorldId, player.CurrentWorldId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Thrown failures (HttpRequestException, parse errors, etc.) are transient: log
                // and skip stamping LastJobDataUpdate so the next startup-seed (or the 7-day
                // stale window) re-queues this player. We deliberately don't re-enqueue
                // immediately — a poisoned profile would block the queue on retry forever.
                _logger.LogWarning(ex, "LodestoneEnrichment: transient fetch failure for {Name} ({Id}); will retry on next seed", player.Name, localContentId);
                return;
            }

            // FetchAsync returns null when NetStone gave us back a null wrapper (HTTP error
            // swallowed inside the library) rather than a confident "Lodestone said no match."
            // Stamping LastJobDataUpdate in that case would lock the row out of retries for a
            // full week on what was probably a transient 5xx, so leave it alone and let the
            // next seed cycle pick it up.
            if (outcome is null)
            {
                _logger.LogInformation("LodestoneEnrichment: inconclusive fetch for {Name} ({Id}); will retry on next seed", player.Name, localContentId);
                return;
            }

            // "Not found on Lodestone" gets stamped so we don't retry every cycle. Free-trial
            // characters and very newly created characters fall into this bucket. The weekly
            // re-enrichment from SeedFromDatabase will pick them up if they ever get indexed.
            if (outcome.NotFound)
            {
                player.LastJobDataUpdate = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("LodestoneEnrichment: {Name}@{World} not found on Lodestone", player.Name, WorldNames.Resolve(player.HomeWorldId) ?? WorldNames.Resolve(player.CurrentWorldId) ?? "?");
                return;
            }

            // Apply whatever we got. Partial successes (e.g. avatar+jobs but minions failed)
            // still update the fields we have.
            var now = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(outcome.AvatarUrl))
                player.AvatarLink = outcome.AvatarUrl;

            if (!string.IsNullOrEmpty(outcome.PortraitUrl))
                player.LodestonePortraitUrl = outcome.PortraitUrl;

            if (outcome.JobLevels is { Count: > 0 })
            {
                player.LodestoneJobData = SerializeJobs(outcome.JobLevels);
                player.MainJobId = outcome.MainJobId;
                player.MainJobLevel = outcome.MainJobLevel;
            }
            // Always stamp LastJobDataUpdate even on a profile-found-but-jobs-missing pass, so
            // we don't busy-loop on the same player.
            player.LastJobDataUpdate = now;

            if (outcome.Minions is { Count: > 0 })
            {
                player.LodestoneMinionsData = JsonSerializer.Serialize(outcome.Minions);
                player.LastMinionsDataUpdate = now;
            }

            if (outcome.Mounts is { Count: > 0 })
            {
                player.LodestoneMountsData = JsonSerializer.Serialize(outcome.Mounts);
                player.LastMountsDataUpdate = now;
            }

            // Mirror avatar + LodestoneId into the PlayerLodestone row so existing readers
            // (PlayerLodestoneDto in profile responses) keep working.
            if (outcome.LodestoneId is int lodestoneId)
            {
                var existing = await db.PlayerLodestones
                    .FirstOrDefaultAsync(pl => pl.PlayerLocalContentId == player.LocalContentId, ct);

                if (existing is null)
                {
                    db.PlayerLodestones.Add(new PlayerLodestone
                    {
                        PlayerLocalContentId = player.LocalContentId,
                        LodestoneId = lodestoneId,
                        AvatarLink = outcome.AvatarUrl,
                    });
                }
                else
                {
                    existing.LodestoneId = lodestoneId;
                    if (!string.IsNullOrEmpty(outcome.AvatarUrl))
                        existing.AvatarLink = outcome.AvatarUrl;
                }
            }

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "LodestoneEnrichment: refreshed {Name} (jobs={Jobs}, minions={Minions}, mounts={Mounts})",
                player.Name,
                outcome.JobLevels?.Count ?? 0,
                outcome.Minions?.Count ?? 0,
                outcome.Mounts?.Count ?? 0);
        }

        private async Task<LodestoneClient> GetClientAsync()
        {
            if (_client is not null) return _client;
            await _clientInit.WaitAsync();
            try
            {
                _client ??= await LodestoneClient.GetClientAsync();
                return _client;
            }
            finally
            {
                _clientInit.Release();
            }
        }

        // Returns:
        //   FetchOutcome with NotFound=false → enrichment data to write
        //   FetchOutcome.NotFoundResult       → confident "Lodestone has no such character"
        //   null                              → inconclusive (NetStone returned a null wrapper,
        //                                       likely a swallowed transient HTTP error). The
        //                                       caller should NOT stamp LastJobDataUpdate.
        private async Task<FetchOutcome?> FetchAsync(string name, short? homeWorldId, short? currentWorldId, CancellationToken ct)
        {
            var client = await GetClientAsync();

            // Prefer the home world for the search filter — current world differs while a
            // character is travelling (Visit destination) but home world is invariant for
            // search. Fall back to current world if home is unset (rare, but happens for
            // very fresh rows where the spawn packet hasn't been seen yet).
            var worldName = WorldNames.Resolve(homeWorldId) ?? WorldNames.Resolve(currentWorldId);

            var query = new CharacterSearchQuery { CharacterName = name };
            if (!string.IsNullOrEmpty(worldName))
                query.World = worldName;

            var primarySearch = await client.SearchCharacter(query).WaitAsync(ct);
            var match = primarySearch?.Results?.FirstOrDefault();

            // Some characters (e.g. cross-server visitors whose home world we have wrong, or
            // whose home world is on a DC the search filter rejects) don't surface on a
            // world-filtered search but do on a global one. Retry without the filter.
            NetStone.Model.Parseables.Search.Character.CharacterSearchPage? fallbackSearch = null;
            if (match is null && !string.IsNullOrEmpty(worldName))
            {
                var globalQuery = new CharacterSearchQuery { CharacterName = name };
                fallbackSearch = await client.SearchCharacter(globalQuery).WaitAsync(ct);
                match = fallbackSearch?.Results?.FirstOrDefault();
            }

            if (match is null)
            {
                // Distinguish "Lodestone responded with no matches" (confident not-found) from
                // "NetStone gave us a null wrapper" (likely a transient 5xx swallowed inside
                // the library). At least one wrapper being non-null means Lodestone *did*
                // answer at some point, so the empty result set is real. If both wrappers we
                // attempted came back null, treat it as inconclusive.
                var anyWrapperNonNull = primarySearch is not null
                    || (fallbackSearch is not null);
                return anyWrapperNonNull ? FetchOutcome.NotFoundResult : null;
            }

            var profile = await client.GetCharacter(match.Id!).WaitAsync(ct);
            if (profile is null)
            {
                // Search found a hit but the profile fetch returned null — this is almost
                // always a transient profile-page failure, not a deleted character. Don't
                // stamp.
                return null;
            }

            int? lodestoneId = int.TryParse(match.Id, out var parsedId) ? parsedId : null;
            var avatarUrl = profile.Avatar?.ToString();

            // Phase 1 — also pull the full-size Lodestone portrait. Prefer NetStone's
            // Portrait property if it exists on this version of the library; otherwise
            // derive from the avatar URL by stripping the size suffix.
            string? portraitUrl;
            try
            {
                // Use reflection to avoid hard-failing if NetStone's API drifts.
                var portraitProp = profile.GetType().GetProperty("Portrait");
                portraitUrl = portraitProp?.GetValue(profile)?.ToString();
            }
            catch
            {
                portraitUrl = null;
            }
            portraitUrl ??= DeriveFullPortraitFromAvatar(avatarUrl);

            var (jobLevels, mainJobId, mainJobLevel) = await TryFetchJobsAsync(profile, name);
            var minions = await TryFetchMinionsAsync(profile, name);
            var mounts = await TryFetchMountsAsync(profile, name);

            return new FetchOutcome(
                NotFound: false,
                LodestoneId: lodestoneId,
                AvatarUrl: avatarUrl,
                PortraitUrl: portraitUrl,
                JobLevels: jobLevels,
                MainJobId: mainJobId,
                MainJobLevel: mainJobLevel,
                Minions: minions,
                Mounts: mounts);
        }

        private async Task<(Dictionary<byte, short>? Levels, byte? MainId, short? MainLevel)> TryFetchJobsAsync(
            NetStone.Model.Parseables.Character.LodestoneCharacter profile, string name)
        {
            try
            {
                var info = await profile.GetClassJobInfo();
                if (info is null) return (null, null, null);

                var levels = new Dictionary<byte, short>();
                byte? mainId = null;
                short? mainLevel = null;

                foreach (var (staticJob, entry) in info.ClassJobDict)
                {
                    if (!entry.IsUnlocked || entry.Level <= 0) continue;
                    var jobId = (byte)(int)staticJob;
                    var level = (short)entry.Level;
                    if (jobId is < 1 or > 100) continue;
                    if (level is < 1 or > 100) continue;

                    levels[jobId] = level;
                    if (mainLevel is null || level > mainLevel)
                    {
                        mainId = jobId;
                        mainLevel = level;
                    }
                }

                return levels.Count > 0 ? (levels, mainId, mainLevel) : (null, null, null);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LodestoneEnrichment: GetClassJobInfo failed for {Name}", name);
                return (null, null, null);
            }
        }

        private async Task<List<MinionInfo>?> TryFetchMinionsAsync(
            NetStone.Model.Parseables.Character.LodestoneCharacter profile, string name)
        {
            try
            {
                var collectable = await profile.GetMinions();
                return collectable is null ? null : ExtractCollectibles<MinionInfo>(collectable);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LodestoneEnrichment: GetMinions failed for {Name}", name);
                return null;
            }
        }

        private async Task<List<MountInfo>?> TryFetchMountsAsync(
            NetStone.Model.Parseables.Character.LodestoneCharacter profile, string name)
        {
            try
            {
                var collectable = await profile.GetMounts();
                return collectable is null ? null : ExtractCollectibles<MountInfo>(collectable);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "LodestoneEnrichment: GetMounts failed for {Name}", name);
                return null;
            }
        }

        // NetStone's CharacterMounts/CharacterMinions don't expose a stable typed surface, so
        // we walk the Collectables enumerable via reflection to read Name, Icon, and Id. This
        // mirrors what the plugin's LodestoneRefreshService already does and tolerates minor
        // NetStone version drift.
        private static List<T> ExtractCollectibles<T>(object collectable) where T : CollectibleBase, new()
        {
            var result = new List<T>();
            var collectablesProp = collectable.GetType().GetProperty("Collectables");
            if (collectablesProp?.GetValue(collectable) is not System.Collections.IEnumerable items)
                return result;

            foreach (var item in items)
            {
                if (item is null) continue;
                var itemType = item.GetType();
                var nameValue = itemType.GetProperty("Name")?.GetValue(item)?.ToString();
                if (string.IsNullOrWhiteSpace(nameValue)) continue;
                var iconValue = itemType.GetProperty("Icon")?.GetValue(item)?.ToString();
                var idValue = itemType.GetProperty("Id")?.GetValue(item)?.ToString();
                int.TryParse(idValue, out var parsedId);

                var entry = new T
                {
                    Name = nameValue,
                    IconUrl = iconValue,
                };
                entry.SetId(parsedId == 0 ? null : parsedId);
                result.Add(entry);
            }
            return result;
        }

        private static string SerializeJobs(Dictionary<byte, short> levels)
        {
            // The wire format is {"<jobId>":<level>, ...} as string keys (matching the existing
            // plugin output), so server-side enrichment doesn't break PlayersController.BuildJobs.
            var asStringKeys = new Dictionary<string, short>(levels.Count);
            foreach (var kvp in levels)
                asStringKeys[kvp.Key.ToString()] = kvp.Value;
            return JsonSerializer.Serialize(asStringKeys);
        }

        // Lodestone serves the same character image at multiple sizes by appending
        // a "_NxN" suffix before the ".jpg" extension. The full unsuffixed URL is
        // the 640x873 portrait. NetStone's Avatar property returns the small one;
        // strip the suffix to get the full one. Return the input unchanged when the
        // pattern doesn't match (defense against Lodestone URL drift).
        public static string? DeriveFullPortraitFromAvatar(string? avatarUrl)
        {
            if (string.IsNullOrEmpty(avatarUrl)) return null;
            // Match "/<hash>_NNxNN.jpg" tail
            var match = System.Text.RegularExpressions.Regex.Match(
                avatarUrl, @"^(.+)_\d+x\d+(\.\w+)$");
            return match.Success ? match.Groups[1].Value + match.Groups[2].Value : avatarUrl;
        }

        private sealed record FetchOutcome(
            bool NotFound,
            int? LodestoneId,
            string? AvatarUrl,
            string? PortraitUrl,
            Dictionary<byte, short>? JobLevels,
            byte? MainJobId,
            short? MainJobLevel,
            List<MinionInfo>? Minions,
            List<MountInfo>? Mounts)
        {
            public static readonly FetchOutcome NotFoundResult =
                new(true, null, null, null, null, null, null, null, null);
        }
    }

    /// <summary>
    /// Wire-compatible with the plugin's MinionInfo/MountInfo: serializes to the same
    /// {Name,IconUrl,MinionId|MountId,AcquiredDate} JSON shape that's already in the database.
    /// </summary>
    public abstract class CollectibleBase
    {
        public string? Name { get; set; }
        public string? IconUrl { get; set; }
        public DateTime? AcquiredDate { get; set; }
        public abstract void SetId(int? id);
    }

    public sealed class MinionInfo : CollectibleBase
    {
        public int? MinionId { get; set; }
        public override void SetId(int? id) => MinionId = id;
    }

    public sealed class MountInfo : CollectibleBase
    {
        public int? MountId { get; set; }
        public override void SetId(int? id) => MountId = id;
    }
}
