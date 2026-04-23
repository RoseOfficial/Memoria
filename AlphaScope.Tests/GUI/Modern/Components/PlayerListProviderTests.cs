using FluentAssertions;
using AlphaScope.Handlers;
using AlphaScope.GUI.Modern.Components;

namespace AlphaScope.Tests.GUI.Modern.Components;

public class PlayerListProviderTests
{
    private static Dictionary<ulong, PersistenceContext.CachedPlayer> SampleCache() => new()
    {
        [1] = new PersistenceContext.CachedPlayer { Name = "Alice Andrews", AccountId = null, HomeWorldId = 73, LastScannedAt = DateTime.FromFileTimeUtc(1_000) },
        [2] = new PersistenceContext.CachedPlayer { Name = "Bob Berkeley",  AccountId = null, HomeWorldId = 73, LastScannedAt = DateTime.FromFileTimeUtc(3_000) },
        [3] = new PersistenceContext.CachedPlayer { Name = "Carla Cortez",  AccountId = null, HomeWorldId = 74, LastScannedAt = DateTime.FromFileTimeUtc(2_000) },
        [4] = new PersistenceContext.CachedPlayer { Name = "Dale Diaz",     AccountId = null, HomeWorldId = 74, LastScannedAt = DateTime.FromFileTimeUtc(4_000) },
    };

    [Fact]
    public void GetRecent_OrdersByLastScannedDescendingAndCaps()
    {
        var result = PlayerListProvider.GetRecent(SampleCache(), limit: 3);

        result.Select(r => r.Name).Should().Equal("Dale Diaz", "Bob Berkeley", "Carla Cortez");
    }

    [Fact]
    public void GetRecent_WithSmallerCacheThanLimit_ReturnsAll()
    {
        var result = PlayerListProvider.GetRecent(SampleCache(), limit: 100);

        result.Should().HaveCount(4);
    }

    [Fact]
    public void GetFavorites_ReturnsOnlyContentIdsInFavorites()
    {
        var favorites = new HashSet<long> { 1L, 3L };

        var result = PlayerListProvider.GetFavorites(SampleCache(), favorites);

        result.Select(r => r.Name).Should().Equal("Carla Cortez", "Alice Andrews");
    }

    [Fact]
    public void GetFavorites_EmptyFavoritesReturnsEmpty()
    {
        var result = PlayerListProvider.GetFavorites(SampleCache(), new HashSet<long>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Search_CaseInsensitiveSubstringMatch()
    {
        var result = PlayerListProvider.Search(SampleCache(), "alice");

        result.Select(r => r.Name).Should().Equal("Alice Andrews");
    }

    [Fact]
    public void Search_MatchesPartialNames()
    {
        var result = PlayerListProvider.Search(SampleCache(), "rt");

        result.Select(r => r.Name).Should().BeEquivalentTo(new[] { "Carla Cortez" });
    }

    [Fact]
    public void Search_EmptyQueryReturnsEmpty()
    {
        var result = PlayerListProvider.Search(SampleCache(), "");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Search_WhitespaceOnlyQueryReturnsEmpty()
    {
        var result = PlayerListProvider.Search(SampleCache(), "   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetRecent_AllNullLastScannedAt_DoesNotThrowAndReturnsAll()
    {
        var cache = new Dictionary<ulong, PersistenceContext.CachedPlayer>
        {
            [1] = new PersistenceContext.CachedPlayer { Name = "Alice", AccountId = null, HomeWorldId = 73, LastScannedAt = null },
            [2] = new PersistenceContext.CachedPlayer { Name = "Bob",   AccountId = null, HomeWorldId = 73, LastScannedAt = null },
        };

        var result = PlayerListProvider.GetRecent(cache, limit: 10);

        result.Should().HaveCount(2);
    }
}
