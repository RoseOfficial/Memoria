using System;
using System.Linq;
using Memoria.Handlers;
using FluentAssertions;
using Xunit;

namespace Memoria.Tests.Handlers;

/// <summary>
/// Serialises any test class that mutates PersistenceContext's static _playerCache.
/// Future test classes that touch _playerCache should also opt into this collection
/// by adding [Collection("PersistenceContextStatic")].
/// </summary>
[CollectionDefinition("PersistenceContextStatic", DisableParallelization = true)]
public class PersistenceContextStaticCollection
{
}

/// <summary>
/// Tests for PersistenceContext.GetAccountAltCharacters.
/// Mutates the static _playerCache; each test clears it before and after to stay isolated.
/// </summary>
[Collection("PersistenceContextStatic")]
public class PersistenceContextAltCharactersTests : IDisposable
{
    public PersistenceContextAltCharactersTests()
    {
        PersistenceContext._playerCache.Clear();
    }

    public void Dispose()
    {
        PersistenceContext._playerCache.Clear();
    }

    private static PersistenceContext.CachedPlayer MakePlayer(string name, ulong? accountId)
        => new()
        {
            AccountId = accountId,
            Name = name,
        };

    [Fact]
    public void GetAccountAltCharacters_ReturnsEmpty_WhenNoOtherPlayersShareAccount()
    {
        PersistenceContext._playerCache[100] = MakePlayer("Solo Alice", accountId: 42);

        var result = PersistenceContext.GetAccountAltCharacters(accountId: 42, excludeContentId: 100);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAccountAltCharacters_ReturnsMatchingAlts()
    {
        PersistenceContext._playerCache[100] = MakePlayer("Main", accountId: 42);
        PersistenceContext._playerCache[101] = MakePlayer("Alt One", accountId: 42);
        PersistenceContext._playerCache[102] = MakePlayer("Alt Two", accountId: 42);

        var result = PersistenceContext.GetAccountAltCharacters(accountId: 42, excludeContentId: 100);

        result.Select(r => r.Player.Name).Should().BeEquivalentTo(new[] { "Alt One", "Alt Two" });
        result.Select(r => r.ContentId).Should().BeEquivalentTo(new ulong[] { 101, 102 });
    }

    [Fact]
    public void GetAccountAltCharacters_ExcludesTargetCharacterItself()
    {
        PersistenceContext._playerCache[100] = MakePlayer("Target", accountId: 42);
        PersistenceContext._playerCache[101] = MakePlayer("Alt", accountId: 42);

        var result = PersistenceContext.GetAccountAltCharacters(accountId: 42, excludeContentId: 100);

        result.Select(r => r.ContentId).Should().ContainSingle().Which.Should().Be(101);
    }

    [Fact]
    public void GetAccountAltCharacters_IgnoresCharactersWithDifferentAccountId()
    {
        PersistenceContext._playerCache[100] = MakePlayer("Target", accountId: 42);
        PersistenceContext._playerCache[101] = MakePlayer("Stranger", accountId: 99);

        var result = PersistenceContext.GetAccountAltCharacters(accountId: 42, excludeContentId: 100);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAccountAltCharacters_IgnoresCharactersWithNullAccountId()
    {
        PersistenceContext._playerCache[100] = MakePlayer("Target", accountId: 42);
        PersistenceContext._playerCache[101] = MakePlayer("Unknown", accountId: null);

        var result = PersistenceContext.GetAccountAltCharacters(accountId: 42, excludeContentId: 100);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAccountAltCharacters_ReturnsEmpty_WhenCacheIsEmpty()
    {
        // Constructor already cleared _playerCache; do not insert anything.

        var result = PersistenceContext.GetAccountAltCharacters(accountId: 42, excludeContentId: 100);

        result.Should().BeEmpty();
    }
}
