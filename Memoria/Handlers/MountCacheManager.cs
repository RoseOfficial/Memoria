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

namespace Memoria.Handlers
{
    public class MountCacheManager : IDisposable
    {
        private readonly HttpClient _httpClient;
        public readonly ConcurrentDictionary<string, MountCacheEntry> _mountCache;
        private readonly ConcurrentDictionary<string, Task> _ongoingDownloads;
        private readonly ConcurrentDictionary<string, int> _failedDownloads;

        public MountCacheManager()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _mountCache = new ConcurrentDictionary<string, MountCacheEntry>();
            _ongoingDownloads = new ConcurrentDictionary<string, Task>();
            _failedDownloads = new ConcurrentDictionary<string, int>();
        }

        public void ClearMountCache()
        {
            foreach (var entry in _mountCache.Values)
            {
                entry.Texture?.Dispose();
            }
            _mountCache.Clear();
        }
        
        /// <summary>
        /// Clears failed downloads count for a specific URL, allowing retry of mount icon download
        /// </summary>
        public void ClearFailedDownloads(string iconUrl)
        {
            _failedDownloads.TryRemove(iconUrl, out _);
        }

        public nint GetMountIconHandle(string iconUrl)
        {
            if (_mountCache != null && _mountCache.TryGetValue(iconUrl, out var cachedEntry))
            {
                if (DateTime.UtcNow < cachedEntry.Expiration && cachedEntry.Texture != null)
                {
                    // Extract nint from ImTextureID using MemoryMarshal
                    var handle = cachedEntry.Texture.Handle;
                    return MemoryMarshal.Cast<Dalamud.Bindings.ImGui.ImTextureID, nint>(MemoryMarshal.CreateSpan(ref handle, 1))[0];
                }

                cachedEntry.Texture?.Dispose();
                _mountCache.TryRemove(iconUrl, out _);
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

                            var newEntry = new MountCacheEntry
                            {
                                Texture = texture,
                                TextureHandle = 0, // Will be computed at render time
                                Expiration = expiration
                            };

                            if (_mountCache != null)
                            {
                                _mountCache[iconUrl] = newEntry;
                            }
                        }
                        else
                        {
                            // Only log the FIRST failure per URL — retries for the same dead URL
                            // would spam the log for a single bad icon.
                            if (!_failedDownloads.ContainsKey(iconUrl))
                                Plugin.Log.Warning($"Failed to download/create texture for mount icon: {iconUrl}");
                            _failedDownloads.AddOrUpdate(iconUrl, 1, (key, oldValue) => oldValue + 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_failedDownloads.ContainsKey(iconUrl))
                            Plugin.Log.Error(ex, $"Error downloading mount icon from {iconUrl}");
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

        private async Task<IDalamudTextureWrap?> DownloadImageAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var imageData = await response.Content.ReadAsByteArrayAsync();

                // Use Dalamud texture service to create texture from image bytes
                var texture = Plugin.TextureProvider.CreateFromImageAsync(imageData).Result;
                return texture;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning($"Failed to download image from {url}: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            foreach (var entry in _mountCache.Values)
            {
                entry.Texture?.Dispose();
            }

            _mountCache.Clear();
            _httpClient?.Dispose();
        }
    }

    public class MountCacheEntry
    {
        public IDalamudTextureWrap? Texture { get; set; }
        public nint TextureHandle { get; set; }
        public DateTime Expiration { get; set; }
    }
}