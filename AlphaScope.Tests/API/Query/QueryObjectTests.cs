using FluentAssertions;
using AlphaScope.API.Query.Player;
using System.Text.Json;

namespace AlphaScope.Tests.API.Query;

public class QueryObjectTests
{
    #region PlayerQueryObject Tests

    [Fact]
    public void PlayerQueryObject_DefaultConstructor_ShouldInitializeWithCorrectDefaults()
    {
        // Act
        var query = new PlayerQueryObject();

        // Assert
        query.LocalContentId.Should().BeNull();
        query.Name.Should().BeNull();
        query.Cursor.Should().Be(0);
        query.IsFetching.Should().BeNull();
        query.F_WorldIds.Should().NotBeNull().And.BeEmpty();
        query.F_MatchAnyPartOfName.Should().BeFalse();
    }

    [Fact]
    public void PlayerQueryObject_ShouldBePublicClass()
    {
        // Arrange
        var playerQueryType = typeof(PlayerQueryObject);

        // Act & Assert
        playerQueryType.IsClass.Should().BeTrue();
        playerQueryType.IsPublic.Should().BeTrue();
        playerQueryType.IsSealed.Should().BeFalse();
    }

    [Fact]
    public void PlayerQueryObject_Properties_ShouldHaveCorrectTypes()
    {
        // Arrange
        var playerQueryType = typeof(PlayerQueryObject);

        // Act & Assert
        var localContentIdProperty = playerQueryType.GetProperty("LocalContentId");
        localContentIdProperty.Should().NotBeNull();
        localContentIdProperty!.PropertyType.Should().Be(typeof(long?));
        localContentIdProperty.CanRead.Should().BeTrue();
        localContentIdProperty.CanWrite.Should().BeTrue();

        var nameProperty = playerQueryType.GetProperty("Name");
        nameProperty.Should().NotBeNull();
        nameProperty!.PropertyType.Should().Be(typeof(string));
        nameProperty.CanRead.Should().BeTrue();
        nameProperty.CanWrite.Should().BeTrue();

        var cursorProperty = playerQueryType.GetProperty("Cursor");
        cursorProperty.Should().NotBeNull();
        cursorProperty!.PropertyType.Should().Be(typeof(int));
        cursorProperty.CanRead.Should().BeTrue();
        cursorProperty.CanWrite.Should().BeTrue();

        var isFetchingProperty = playerQueryType.GetProperty("IsFetching");
        isFetchingProperty.Should().NotBeNull();
        isFetchingProperty!.PropertyType.Should().Be(typeof(bool?));
        isFetchingProperty.CanRead.Should().BeTrue();
        isFetchingProperty.CanWrite.Should().BeTrue();

        var worldIdsProperty = playerQueryType.GetProperty("F_WorldIds");
        worldIdsProperty.Should().NotBeNull();
        worldIdsProperty!.PropertyType.Should().Be(typeof(List<short>));
        worldIdsProperty.CanRead.Should().BeTrue();
        worldIdsProperty.CanWrite.Should().BeTrue();

        var matchAnyPartProperty = playerQueryType.GetProperty("F_MatchAnyPartOfName");
        matchAnyPartProperty.Should().NotBeNull();
        matchAnyPartProperty!.PropertyType.Should().Be(typeof(bool?));
        matchAnyPartProperty.CanRead.Should().BeTrue();
        matchAnyPartProperty.CanWrite.Should().BeTrue();
    }

    [Fact]
    public void PlayerQueryObject_LocalContentId_ShouldAcceptValidValues()
    {
        // Arrange
        var query = new PlayerQueryObject();

        // Act & Assert
        query.LocalContentId = null;
        query.LocalContentId.Should().BeNull();

        query.LocalContentId = 0L;
        query.LocalContentId.Should().Be(0L);

        query.LocalContentId = 12345678901234567L;
        query.LocalContentId.Should().Be(12345678901234567L);

        query.LocalContentId = long.MaxValue;
        query.LocalContentId.Should().Be(long.MaxValue);

        query.LocalContentId = long.MinValue;
        query.LocalContentId.Should().Be(long.MinValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TestPlayer")]
    [InlineData("Player With Spaces")]
    [InlineData("áéíóú")]
    [InlineData("日本語")]
    public void PlayerQueryObject_Name_ShouldAcceptValidValues(string? name)
    {
        // Arrange
        var query = new PlayerQueryObject();

        // Act
        query.Name = name;

        // Assert
        query.Name.Should().Be(name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void PlayerQueryObject_Cursor_ShouldAcceptValidValues(int cursor)
    {
        // Arrange
        var query = new PlayerQueryObject();

        // Act
        query.Cursor = cursor;

        // Assert
        query.Cursor.Should().Be(cursor);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PlayerQueryObject_IsFetching_ShouldAcceptValidValues(bool isFetching)
    {
        // Arrange
        var query = new PlayerQueryObject();

        // Act
        query.IsFetching = isFetching;

        // Assert
        query.IsFetching.Should().Be(isFetching);
    }

    [Fact]
    public void PlayerQueryObject_F_WorldIds_ShouldBeModifiable()
    {
        // Arrange
        var query = new PlayerQueryObject();

        // Act
        query.F_WorldIds!.Add(65); // Malboro
        query.F_WorldIds!.Add(66); // Hyperion
        query.F_WorldIds!.AddRange(new short[] { 67, 68, 69 });

        // Assert
        query.F_WorldIds.Should().HaveCount(5);
        query.F_WorldIds.Should().Contain(new short[] { 65, 66, 67, 68, 69 });
    }

    [Fact]
    public void PlayerQueryObject_F_WorldIds_ShouldAllowEmpty()
    {
        // Arrange
        var query = new PlayerQueryObject();

        // Act
        query.F_WorldIds!.Clear();

        // Assert
        query.F_WorldIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void PlayerQueryObject_F_MatchAnyPartOfName_ShouldAcceptValidValues(bool? matchAnyPart)
    {
        // Arrange
        var query = new PlayerQueryObject();

        // Act
        query.F_MatchAnyPartOfName = matchAnyPart;

        // Assert
        query.F_MatchAnyPartOfName.Should().Be(matchAnyPart);
    }

    [Fact]
    public void PlayerQueryObject_ShouldBeSerializableToJson()
    {
        // Arrange
        var query = new PlayerQueryObject
        {
            LocalContentId = 12345678901234567L,
            Name = "TestPlayer",
            Cursor = 10,
            IsFetching = true,
            F_WorldIds = new List<short> { 65, 66, 67 },
            F_MatchAnyPartOfName = true
        };

        // Act
        var json = JsonSerializer.Serialize(query);
        var deserializedQuery = JsonSerializer.Deserialize<PlayerQueryObject>(json);

        // Assert
        deserializedQuery.Should().NotBeNull();
        deserializedQuery!.LocalContentId.Should().Be(query.LocalContentId);
        deserializedQuery.Name.Should().Be(query.Name);
        deserializedQuery.Cursor.Should().Be(query.Cursor);
        deserializedQuery.IsFetching.Should().Be(query.IsFetching);
        deserializedQuery.F_WorldIds.Should().BeEquivalentTo(query.F_WorldIds);
        deserializedQuery.F_MatchAnyPartOfName.Should().Be(query.F_MatchAnyPartOfName);
    }

    #endregion
}
