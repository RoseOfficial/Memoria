namespace TestUtilities;

public static class TestDataBuilders
{
    public static PlayerTestDataBuilder CreatePlayer() => new();
    public static RetainerTestDataBuilder CreateRetainer() => new();
    public static UserTestDataBuilder CreateUser() => new();
}

public class PlayerTestDataBuilder
{
    private uint _contentId = 12345678;
    private string _name = "TestPlayer";
    private uint _worldId = 65;
    private ulong _accountId = 1001;
    private DateTime _seenDate = DateTime.UtcNow;

    public PlayerTestDataBuilder WithContentId(uint contentId)
    {
        _contentId = contentId;
        return this;
    }

    public PlayerTestDataBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public PlayerTestDataBuilder WithWorldId(uint worldId)
    {
        _worldId = worldId;
        return this;
    }

    public PlayerTestDataBuilder WithAccountId(ulong accountId)
    {
        _accountId = accountId;
        return this;
    }

    public PlayerTestDataBuilder WithSeenDate(DateTime seenDate)
    {
        _seenDate = seenDate;
        return this;
    }

    public dynamic Build() => new
    {
        ContentId = _contentId,
        Name = _name,
        WorldId = _worldId,
        AccountId = _accountId,
        SeenDate = _seenDate,
        PlayerCustomizeData = new byte[26],
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow
    };
}

public class RetainerTestDataBuilder
{
    private ulong _retainerId = 98765432;
    private string _name = "TestRetainer";
    private uint _worldId = 65;
    private uint _ownerContentId = 12345678;
    private DateTime _seenDate = DateTime.UtcNow;

    public RetainerTestDataBuilder WithRetainerId(ulong retainerId)
    {
        _retainerId = retainerId;
        return this;
    }

    public RetainerTestDataBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RetainerTestDataBuilder WithWorldId(uint worldId)
    {
        _worldId = worldId;
        return this;
    }

    public RetainerTestDataBuilder WithOwnerContentId(uint ownerContentId)
    {
        _ownerContentId = ownerContentId;
        return this;
    }

    public RetainerTestDataBuilder WithSeenDate(DateTime seenDate)
    {
        _seenDate = seenDate;
        return this;
    }

    public dynamic Build() => new
    {
        RetainerId = _retainerId,
        Name = _name,
        WorldId = _worldId,
        OwnerContentId = _ownerContentId,
        SeenDate = _seenDate,
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow
    };
}

public class UserTestDataBuilder
{
    private string _key = "test-key-123";
    private ulong _gameAccountId = 1001;
    private string _lodestoneId = "12345";
    private bool _isPublic = true;

    public UserTestDataBuilder WithKey(string key)
    {
        _key = key;
        return this;
    }

    public UserTestDataBuilder WithGameAccountId(ulong gameAccountId)
    {
        _gameAccountId = gameAccountId;
        return this;
    }

    public UserTestDataBuilder WithLodestoneId(string lodestoneId)
    {
        _lodestoneId = lodestoneId;
        return this;
    }

    public UserTestDataBuilder WithIsPublic(bool isPublic)
    {
        _isPublic = isPublic;
        return this;
    }

    public dynamic Build() => new
    {
        Key = _key,
        GameAccountId = _gameAccountId,
        LodestoneId = _lodestoneId,
        IsPublic = _isPublic,
        Created = DateTime.UtcNow,
        Updated = DateTime.UtcNow
    };
}