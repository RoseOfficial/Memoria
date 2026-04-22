using System;
using System.Collections.Generic;
using System.IO;
using AlphaScope.API.Models.Requests.Player;
using Newtonsoft.Json;

namespace AlphaScope.Persistence;

/// <summary>
/// Append-only JSONL outbox for pending player uploads. Survives plugin crashes so scans
/// are not lost between capture and server POST. This is a write-ahead log for the upload
/// pipeline, not a database: one <see cref="PostPlayerRequest"/> per line, written when a
/// scan is enqueued, dropped after a successful upload, replayed on plugin start.
/// </summary>
public sealed class UploadOutbox
{
    private const long DefaultMaxBytes = 10L * 1024 * 1024;

    private readonly string _path;
    private readonly long _maxBytes;
    private readonly object _writeLock = new();

    public UploadOutbox(string path, long maxBytes = DefaultMaxBytes)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _maxBytes = maxBytes > 0 ? maxBytes : DefaultMaxBytes;

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>Appends a pending upload to disk.</summary>
    public void Append(PostPlayerRequest request)
    {
        if (request is null) return;

        var line = JsonConvert.SerializeObject(request) + "\n";
        lock (_writeLock)
        {
            EnforceCap();
            File.AppendAllText(_path, line);
        }
    }

    /// <summary>Streams every pending entry currently on disk.</summary>
    public IEnumerable<PostPlayerRequest> ReadPending()
    {
        if (!File.Exists(_path))
            yield break;

        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            PostPlayerRequest? parsed = null;
            try
            {
                parsed = JsonConvert.DeserializeObject<PostPlayerRequest>(line);
            }
            catch (JsonException)
            {
                // Skip malformed lines rather than poison the whole outbox.
            }

            if (parsed != null)
                yield return parsed;
        }
    }

    /// <summary>
    /// Drops any pending entries whose LocalContentId is in the supplied set.
    /// Rewrites the file atomically via a temp-file + replace.
    /// </summary>
    public void Remove(IEnumerable<ulong> contentIds)
    {
        lock (_writeLock)
        {
            if (!File.Exists(_path)) return;

            var drop = new HashSet<ulong>(contentIds);
            if (drop.Count == 0) return;

            var tempPath = _path + ".tmp";
            using (var writer = new StreamWriter(tempPath, append: false))
            {
                foreach (var line in File.ReadLines(_path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var shouldKeep = true;
                    try
                    {
                        var parsed = JsonConvert.DeserializeObject<PostPlayerRequest>(line);
                        if (parsed != null && drop.Contains(parsed.LocalContentId))
                            shouldKeep = false;
                    }
                    catch (JsonException)
                    {
                        // Preserve unparseable lines so we never silently destroy data.
                    }

                    if (shouldKeep)
                        writer.WriteLine(line);
                }
            }
            File.Move(tempPath, _path, overwrite: true);
        }
    }

    /// <summary>
    /// If the outbox has grown past the cap, discards the oldest entries until it fits.
    /// Entries are removed from the head because newer scans carry more current data.
    /// </summary>
    private void EnforceCap()
    {
        if (!File.Exists(_path)) return;

        var info = new FileInfo(_path);
        if (info.Length <= _maxBytes) return;

        var lines = File.ReadAllLines(_path);
        var kept = new List<string>();
        long running = 0;

        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var lineLength = lines[i].Length + 1;
            if (running + lineLength > _maxBytes) break;
            running += lineLength;
            kept.Insert(0, lines[i]);
        }

        File.WriteAllLines(_path, kept);
    }
}
