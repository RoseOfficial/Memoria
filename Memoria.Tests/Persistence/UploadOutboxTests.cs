using System;
using System.IO;
using System.Linq;
using Memoria.API.Models.Requests.Player;
using Memoria.Persistence;
using Xunit;

namespace Memoria.Tests.Persistence;

public sealed class UploadOutboxTests : IDisposable
{
    private readonly string _tempDir;

    public UploadOutboxTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "memoria-outbox-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private string PathFor(string name) => Path.Combine(_tempDir, name);

    [Fact]
    public void Append_ThenReadPending_ReturnsAppendedEntries()
    {
        var outbox = new UploadOutbox(PathFor("pending.jsonl"));

        outbox.Append(new PostPlayerRequest
        {
            LocalContentId = 42,
            Name = "Testy McTestface",
            CreatedAt = 1700000000,
        });

        var pending = outbox.ReadPending().ToList();

        Assert.Single(pending);
        Assert.Equal(42ul, pending[0].LocalContentId);
        Assert.Equal("Testy McTestface", pending[0].Name);
    }

    [Fact]
    public void Append_MultipleEntries_PreservesInsertionOrder()
    {
        var outbox = new UploadOutbox(PathFor("ordered.jsonl"));

        outbox.Append(new PostPlayerRequest { LocalContentId = 1, Name = "A", CreatedAt = 1 });
        outbox.Append(new PostPlayerRequest { LocalContentId = 2, Name = "B", CreatedAt = 2 });
        outbox.Append(new PostPlayerRequest { LocalContentId = 3, Name = "C", CreatedAt = 3 });

        var ids = outbox.ReadPending().Select(p => p.LocalContentId).ToArray();

        Assert.Equal(new ulong[] { 1, 2, 3 }, ids);
    }

    [Fact]
    public void Remove_DropsOnlyMatchingIds_KeepsOthersInOrder()
    {
        var outbox = new UploadOutbox(PathFor("remove.jsonl"));
        outbox.Append(new PostPlayerRequest { LocalContentId = 1, Name = "A", CreatedAt = 1 });
        outbox.Append(new PostPlayerRequest { LocalContentId = 2, Name = "B", CreatedAt = 2 });
        outbox.Append(new PostPlayerRequest { LocalContentId = 3, Name = "C", CreatedAt = 3 });

        outbox.Remove(new ulong[] { 2 });

        var ids = outbox.ReadPending().Select(p => p.LocalContentId).ToArray();
        Assert.Equal(new ulong[] { 1, 3 }, ids);
    }

    [Fact]
    public void Remove_WithNoMatches_LeavesFileUnchanged()
    {
        var path = PathFor("no-match.jsonl");
        var outbox = new UploadOutbox(path);
        outbox.Append(new PostPlayerRequest { LocalContentId = 1, Name = "A", CreatedAt = 1 });
        outbox.Append(new PostPlayerRequest { LocalContentId = 2, Name = "B", CreatedAt = 2 });

        outbox.Remove(new ulong[] { 99, 100 });

        var ids = outbox.ReadPending().Select(p => p.LocalContentId).ToArray();
        Assert.Equal(new ulong[] { 1, 2 }, ids);
    }

    [Fact]
    public void ReadPending_OnMissingFile_ReturnsEmpty()
    {
        var outbox = new UploadOutbox(PathFor("nonexistent.jsonl"));
        Assert.Empty(outbox.ReadPending());
    }

    [Fact]
    public void Append_PastSizeCap_DropsOldestAndKeepsNewest()
    {
        // Tiny synthetic cap so we hit it with a handful of entries.
        var outbox = new UploadOutbox(PathFor("capped.jsonl"), maxBytes: 512);

        for (int i = 0; i < 100; i++)
        {
            outbox.Append(new PostPlayerRequest
            {
                LocalContentId = (ulong)i,
                Name = new string('x', 32),
                CreatedAt = i,
            });
        }

        var ids = outbox.ReadPending().Select(p => p.LocalContentId).ToList();

        // Older entries should have been dropped; the newest must survive.
        Assert.True(ids.Count < 100, $"expected some entries dropped, got {ids.Count}");
        Assert.Contains(99ul, ids);
    }

    [Fact]
    public void ReadPending_SkipsMalformedLines()
    {
        var path = PathFor("malformed.jsonl");
        File.WriteAllLines(path, new[]
        {
            "not-valid-json",
            "{\"1\":5,\"2\":\"valid\",\"CreatedAt\":1}", // JsonProperty 1=LocalContentId, 2=Name
            "{broken",
        });

        var outbox = new UploadOutbox(path);
        var ids = outbox.ReadPending().Select(p => p.LocalContentId).ToList();

        Assert.Single(ids);
        Assert.Equal(5ul, ids[0]);
    }
}
