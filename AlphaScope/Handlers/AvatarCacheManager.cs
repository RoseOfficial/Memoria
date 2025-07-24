using Dalamud.Interface.Textures.TextureWraps;
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
    public class AvatarCacheManager : IDisposable
    {
        private readonly HttpClient _httpClient;
        public readonly ConcurrentDictionary<string, AvatarCacheEntry> _avatarCache;
        private readonly ConcurrentDictionary<string, Task> _ongoingDownloads;
        private readonly ConcurrentDictionary<string, int> _failedDownloads;

        public AvatarCacheManager()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _avatarCache = new ConcurrentDictionary<string, AvatarCacheEntry>();
            _ongoingDownloads = new ConcurrentDictionary<string, Task>();
            _failedDownloads = new ConcurrentDictionary<string, int>();
        }

        public void ClearAvatarCache()
        {
            foreach (var entry in _avatarCache.Values)
            {
                entry.Texture?.Dispose();
            }
            _avatarCache.Clear();
        }

        public nint GetAvatarHandle(string avatarUrl)
        {
            if (_avatarCache != null && _avatarCache.TryGetValue(avatarUrl, out var cachedEntry))
            {
                if (DateTime.UtcNow < cachedEntry.Expiration)
                    return cachedEntry.TextureHandle;

                cachedEntry.Texture?.Dispose();
                _avatarCache.TryRemove(avatarUrl, out _);
            }

            // Check if this URL has failed too many times recently
            if (_failedDownloads.TryGetValue(avatarUrl, out var failCount) && failCount >= 3)
            {
                Plugin.Log.Debug($"Skipping avatar download - too many failures: {avatarUrl}");
                return 0;
            }

            if (!_ongoingDownloads.ContainsKey(avatarUrl))
            {
                _ongoingDownloads[avatarUrl] = Task.Run(async () =>
                {
                    try
                    {
                        var texture = await DownloadImageAsync(avatarUrl);

                        if (texture != null)
                        {
                            var expiration = DateTime.UtcNow.AddMinutes(60);

                            var newEntry = new AvatarCacheEntry
                            {
                                Texture = texture,
                                TextureHandle = texture.ImGuiHandle,
                                Expiration = expiration
                            };

                            if (_avatarCache != null)
                            {
                                _avatarCache[avatarUrl] = newEntry;
                                Plugin.Log.Debug($"Avatar cached successfully: {avatarUrl}");
                            }
                        }
                        else
                        {
                            Plugin.Log.Warning($"Failed to download/create texture for avatar: {avatarUrl}");
                            // Track failed downloads
                            _failedDownloads.AddOrUpdate(avatarUrl, 1, (key, oldValue) => oldValue + 1);
                        }
                    }
                    catch (Exception ex) 
                    {
                        Plugin.Log.Error($"Unexpected error in avatar download task for {avatarUrl}: {ex.Message}");
                        // Track failed downloads
                        _failedDownloads.AddOrUpdate(avatarUrl, 1, (key, oldValue) => oldValue + 1);
                    }
                    finally
                    {
                        _ongoingDownloads.TryRemove(avatarUrl, out _);
                    }
                });
            }

            return 0;
        }


        private async Task<IDalamudTextureWrap> DownloadImageAsync(string avatarUrl)
        {
            try
            {
                Plugin.Log.Debug($"Attempting to download avatar from: {avatarUrl}");
                var imageData = await _httpClient.GetByteArrayAsync(avatarUrl);

                if (imageData == null || imageData.Length == 0)
                {
                    Plugin.Log.Warning($"Avatar download returned empty data for URL: {avatarUrl}");
                    return null;
                }

                Plugin.Log.Debug($"Downloaded {imageData.Length} bytes for avatar: {avatarUrl}");

                var imageMemory = new ReadOnlyMemory<byte>(imageData);

                var texture = await Plugin.TextureProvider.CreateFromImageAsync(imageMemory);

                if (texture == null)
                {
                    Plugin.Log.Warning($"Failed to create texture from image data for URL: {avatarUrl}");
                }
                else
                {
                    Plugin.Log.Debug($"Successfully created texture for avatar: {avatarUrl}");
                }

                return texture;
            }
            catch (HttpRequestException httpEx) 
            {
                Plugin.Log.Error($"HTTP error downloading avatar from {avatarUrl}: {httpEx.Message}");
                // Track failed downloads
                _failedDownloads.AddOrUpdate(avatarUrl, 1, (key, oldValue) => oldValue + 1);
            }
            catch (Exception ex) 
            {
                Plugin.Log.Error($"Error downloading avatar from {avatarUrl}: {ex.Message}");
                // Track failed downloads
                _failedDownloads.AddOrUpdate(avatarUrl, 1, (key, oldValue) => oldValue + 1);
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var entry in _avatarCache.Values)
            {
                entry.Texture?.Dispose();
            }

            _httpClient.Dispose();
        }

        public class AvatarCacheEntry
        {
            public IDalamudTextureWrap Texture { get; set; } = null!;
            public nint TextureHandle { get; set; }
            public DateTime Expiration { get; set; }
        }
    }
}
