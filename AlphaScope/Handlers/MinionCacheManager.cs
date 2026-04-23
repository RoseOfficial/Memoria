using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Textures;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AlphaScope.Handlers
{
    public class MinionCacheManager : IDisposable
    {
        private readonly HttpClient _httpClient;
        public readonly ConcurrentDictionary<string, MinionCacheEntry> _minionCache;
        private readonly ConcurrentDictionary<string, Task> _ongoingDownloads;
        private readonly ConcurrentDictionary<string, int> _failedDownloads;

        public MinionCacheManager()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _minionCache = new ConcurrentDictionary<string, MinionCacheEntry>();
            _ongoingDownloads = new ConcurrentDictionary<string, Task>();
            _failedDownloads = new ConcurrentDictionary<string, int>();
        }

        public void ClearMinionCache()
        {
            foreach (var entry in _minionCache.Values)
            {
                entry.Texture?.Dispose();
            }
            _minionCache.Clear();
        }
        
        /// <summary>
        /// Clears failed downloads count for a specific URL, allowing retry of minion icon download
        /// </summary>
        public void ClearFailedDownloads(string iconUrl)
        {
            _failedDownloads.TryRemove(iconUrl, out _);
        }

        public nint GetMinionIconHandle(string iconUrl)
        {
            if (_minionCache != null && _minionCache.TryGetValue(iconUrl, out var cachedEntry))
            {
                if (DateTime.UtcNow < cachedEntry.Expiration && cachedEntry.Texture != null)
                {
                    // Extract nint from ImTextureID using MemoryMarshal
                    var handle = cachedEntry.Texture.Handle;
                    return MemoryMarshal.Cast<Dalamud.Bindings.ImGui.ImTextureID, nint>(MemoryMarshal.CreateSpan(ref handle, 1))[0];
                }

                cachedEntry.Texture?.Dispose();
                _minionCache.TryRemove(iconUrl, out _);
            }

            // Check if this URL has failed too many times recently
            if (_failedDownloads.TryGetValue(iconUrl, out var failCount) && failCount >= 3)
            {
                return 0;
            }

            if (!_ongoingDownloads.ContainsKey(iconUrl))
            {
                _ongoingDownloads[iconUrl] = Task.Run(async () =>
                {
                    try
                    {
                        var texture = await DownloadImageAsync(iconUrl);

                        if (texture != null)
                        {
                            var expiration = DateTime.UtcNow.AddMinutes(60);

                            var newEntry = new MinionCacheEntry
                            {
                                Texture = texture,
                                TextureHandle = 0, // Will be computed at render time
                                Expiration = expiration
                            };

                            if (_minionCache != null)
                            {
                                _minionCache[iconUrl] = newEntry;
                            }
                        }
                        else
                        {
                            // Only log the FIRST failure per URL — after that the entry is cached
                            // in _failedDownloads and the UI keeps asking, so repeated logging
                            // spams the user's log for a single bad icon.
                            if (!_failedDownloads.ContainsKey(iconUrl))
                                Plugin.Log.Warning($"Failed to download/create texture for minion icon: {iconUrl}");
                            _failedDownloads.AddOrUpdate(iconUrl, 1, (key, oldValue) => oldValue + 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_failedDownloads.ContainsKey(iconUrl))
                            Plugin.Log.Error($"Unexpected error in minion icon download task for {iconUrl}: {ex.Message}");
                        _failedDownloads.AddOrUpdate(iconUrl, 1, (key, oldValue) => oldValue + 1);
                    }
                    finally
                    {
                        _ongoingDownloads.TryRemove(iconUrl, out _);
                    }
                });
            }

            return 0;
        }

        private async Task<IDalamudTextureWrap?> DownloadImageAsync(string iconUrl)
        {
            try
            {
                var imageData = await _httpClient.GetByteArrayAsync(iconUrl);

                if (imageData == null || imageData.Length == 0)
                {
                    Plugin.Log.Warning($"Minion icon download returned empty data for URL: {iconUrl}");
                    return null;
                }

                var imageMemory = new ReadOnlyMemory<byte>(imageData);

                var texture = await Plugin.TextureProvider.CreateFromImageAsync(imageMemory);

                if (texture == null)
                {
                    Plugin.Log.Warning($"Failed to create texture from image data for URL: {iconUrl}");
                }

                return texture;
            }
            catch (HttpRequestException httpEx)
            {
                if (!_failedDownloads.ContainsKey(iconUrl))
                    Plugin.Log.Warning($"Minion icon download failed ({httpEx.StatusCode}): {iconUrl}");
                _failedDownloads.AddOrUpdate(iconUrl, 1, (key, oldValue) => oldValue + 1);
            }
            catch (Exception ex)
            {
                if (!_failedDownloads.ContainsKey(iconUrl))
                    Plugin.Log.Error($"Error downloading minion icon from {iconUrl}: {ex.Message}");
                _failedDownloads.AddOrUpdate(iconUrl, 1, (key, oldValue) => oldValue + 1);
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var entry in _minionCache.Values)
            {
                entry.Texture?.Dispose();
            }

            _httpClient.Dispose();
        }

        public class MinionCacheEntry
        {
            public IDalamudTextureWrap Texture { get; set; } = null!;
            public nint TextureHandle { get; set; }
            public DateTime Expiration { get; set; }
        }
    }
}