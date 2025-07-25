using FluentAssertions;
using AlphaScope.API.Query;
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
        query.IsFetching.Should().BeFalse();
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
        isFetchingProperty!.PropertyType.Should().Be(typeof(bool));
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
        query.F_WorldIds.Add(65); // Malboro
        query.F_WorldIds.Add(66); // Hyperion
        query.F_WorldIds.AddRange(new short[] { 67, 68, 69 });

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
        query.F_WorldIds.Clear();

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
            F_WorldIds = { 65, 66, 67 },
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

    #region RetainerQueryObject Tests

    [Fact]
    public void RetainerQueryObject_DefaultConstructor_ShouldInitializeWithCorrectDefaults()
    {
        // Act
        var query = new RetainerQueryObject();

        // Assert
        query.Name.Should().BeNull();
        query.Cursor.Should().Be(0);
        query.IsFetching.Should().BeFalse();
        query.F_WorldIds.Should().NotBeNull().And.BeEmpty();
        query.F_MatchAnyPartOfName.Should().BeFalse();
    }

    [Fact]
    public void RetainerQueryObject_ShouldBePublicClass()
    {
        // Arrange
        var retainerQueryType = typeof(RetainerQueryObject);

        // Act & Assert
        retainerQueryType.IsClass.Should().BeTrue();
        retainerQueryType.IsPublic.Should().BeTrue();
        retainerQueryType.IsSealed.Should().BeFalse();
    }

    [Fact]
    public void RetainerQueryObject_Properties_ShouldHaveCorrectTypes()
    {
        // Arrange
        var retainerQueryType = typeof(RetainerQueryObject);

        // Act & Assert
        var nameProperty = retainerQueryType.GetProperty("Name");
        nameProperty.Should().NotBeNull();
        nameProperty!.PropertyType.Should().Be(typeof(string));
        nameProperty.CanRead.Should().BeTrue();
        nameProperty.CanWrite.Should().BeTrue();

        var cursorProperty = retainerQueryType.GetProperty("Cursor");
        cursorProperty.Should().NotBeNull();
        cursorProperty!.PropertyType.Should().Be(typeof(int));
        cursorProperty.CanRead.Should().BeTrue();
        cursorProperty.CanWrite.Should().BeTrue();

        var isFetchingProperty = retainerQueryType.GetProperty("IsFetching");
        isFetchingProperty.Should().NotBeNull();
        isFetchingProperty!.PropertyType.Should().Be(typeof(bool));
        isFetchingProperty.CanRead.Should().BeTrue();
        isFetchingProperty.CanWrite.Should().BeTrue();

        var worldIdsProperty = retainerQueryType.GetProperty("F_WorldIds");
        worldIdsProperty.Should().NotBeNull();
        worldIdsProperty!.PropertyType.Should().Be(typeof(List<short>));
        worldIdsProperty.CanRead.Should().BeTrue();
        worldIdsProperty.CanWrite.Should().BeTrue();

        var matchAnyPartProperty = retainerQueryType.GetProperty("F_MatchAnyPartOfName");
        matchAnyPartProperty.Should().NotBeNull();
        matchAnyPartProperty!.PropertyType.Should().Be(typeof(bool?));
        matchAnyPartProperty.CanRead.Should().BeTrue();
        matchAnyPartProperty.CanWrite.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TestRetainer")]
    [InlineData("Retainer With Spaces")]
    [InlineData("áéíóú")]
    [InlineData("日本語")]
    public void RetainerQueryObject_Name_ShouldAcceptValidValues(string? name)
    {
        // Arrange
        var query = new RetainerQueryObject();

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
    public void RetainerQueryObject_Cursor_ShouldAcceptValidValues(int cursor)
    {
        // Arrange
        var query = new RetainerQueryObject();

        // Act
        query.Cursor = cursor;

        // Assert
        query.Cursor.Should().Be(cursor);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RetainerQueryObject_IsFetching_ShouldAcceptValidValues(bool isFetching)
    {
        // Arrange
        var query = new RetainerQueryObject();

        // Act
        query.IsFetching = isFetching;

        // Assert
        query.IsFetching.Should().Be(isFetching);
    }

    [Fact]
    public void RetainerQueryObject_F_WorldIds_ShouldBeModifiable()
    {
        // Arrange
        var query = new RetainerQueryObject();

        // Act
        query.F_WorldIds.Add(65); // Malboro
        query.F_WorldIds.Add(66); // Hyperion
        query.F_WorldIds.AddRange(new short[] { 67, 68, 69 });

        // Assert
        query.F_WorldIds.Should().HaveCount(5);
        query.F_WorldIds.Should().Contain(new short[] { 65, 66, 67, 68, 69 });
    }

    [Fact]
    public void RetainerQueryObject_F_WorldIds_ShouldAllowEmpty()
    {
        // Arrange
        var query = new RetainerQueryObject();

        // Act
        query.F_WorldIds.Clear();

        // Assert
        query.F_WorldIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void RetainerQueryObject_F_MatchAnyPartOfName_ShouldAcceptValidValues(bool? matchAnyPart)
    {
        // Arrange
        var query = new RetainerQueryObject();

        // Act
        query.F_MatchAnyPartOfName = matchAnyPart;

        // Assert
        query.F_MatchAnyPartOfName.Should().Be(matchAnyPart);
    }

    [Fact]
    public void RetainerQueryObject_ShouldBeSerializableToJson()
    {
        // Arrange
        var query = new RetainerQueryObject
        {
            Name = "TestRetainer",
            Cursor = 10,
            IsFetching = true,
            F_WorldIds = { 65, 66, 67 },
            F_MatchAnyPartOfName = true
        };

        // Act
        var json = JsonSerializer.Serialize(query);
        var deserializedQuery = JsonSerializer.Deserialize<RetainerQueryObject>(json);

        // Assert
        deserializedQuery.Should().NotBeNull();
        deserializedQuery!.Name.Should().Be(query.Name);
        deserializedQuery.Cursor.Should().Be(query.Cursor);
        deserializedQuery.IsFetching.Should().Be(query.IsFetching);
        deserializedQuery.F_WorldIds.Should().BeEquivalentTo(query.F_WorldIds);
        deserializedQuery.F_MatchAnyPartOfName.Should().Be(query.F_MatchAnyPartOfName);
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void PlayerQueryObject_Vs_RetainerQueryObject_ShouldHaveSimilarStructure()
    {
        // Arrange
        var playerQueryType = typeof(PlayerQueryObject);
        var retainerQueryType = typeof(RetainerQueryObject);

        var playerProperties = playerQueryType.GetProperties();
        var retainerProperties = retainerQueryType.GetProperties();

        // Act & Assert
        // Both should have Name, Cursor, IsFetching, F_WorldIds, F_MatchAnyPartOfName
        var commonPropertyNames = new[] { "Name", "Cursor", "IsFetching", "F_WorldIds", "F_MatchAnyPartOfName" };
        
        foreach (var propertyName in commonPropertyNames)
        {
            var playerProperty = playerProperties.FirstOrDefault(p => p.Name == propertyName);
            var retainerProperty = retainerProperties.FirstOrDefault(p => p.Name == propertyName);

            playerProperty.Should().NotBeNull($"PlayerQueryObject should have {propertyName} property");
            retainerProperty.Should().NotBeNull($"RetainerQueryObject should have {propertyName} property");

            if (propertyName != "Name") // Name types might differ
            {
                playerProperty!.PropertyType.Should().Be(retainerProperty!.PropertyType, 
                    $"{propertyName} should have the same type in both query objects");
            }
        }

        // PlayerQueryObject should have additional LocalContentId property
        var localContentIdProperty = playerProperties.FirstOrDefault(p => p.Name == "LocalContentId");
        localContentIdProperty.Should().NotBeNull("PlayerQueryObject should have LocalContentId property");

        // RetainerQueryObject should not have LocalContentId
        var retainerLocalContentId = retainerProperties.FirstOrDefault(p => p.Name == "LocalContentId");
        retainerLocalContentId.Should().BeNull("RetainerQueryObject should not have LocalContentId property");
    }

    [Fact]
    public void QueryObjects_ShouldSupportQueryByWorldFiltering()
    {
        // Arrange
        var playerQuery = new PlayerQueryObject();
        var retainerQuery = new RetainerQueryObject();

        var testWorldIds = new short[] { 65, 66, 67, 68, 69 }; // Sample world IDs

        // Act
        playerQuery.F_WorldIds.AddRange(testWorldIds);
        retainerQuery.F_WorldIds.AddRange(testWorldIds);

        // Assert
        playerQuery.F_WorldIds.Should().BeEquivalentTo(testWorldIds);
        retainerQuery.F_WorldIds.Should().BeEquivalentTo(testWorldIds);
    }

    [Fact]
    public void QueryObjects_ShouldSupportPartialNameMatching()
    {
        // Arrange
        var playerQuery = new PlayerQueryObject();
        var retainerQuery = new RetainerQueryObject();

        // Act
        playerQuery.F_MatchAnyPartOfName = true;
        retainerQuery.F_MatchAnyPartOfName = true;

        // Assert
        playerQuery.F_MatchAnyPartOfName.Should().BeTrue();
        retainerQuery.F_MatchAnyPartOfName.Should().BeTrue();

        // Act - Test nullable behavior
        playerQuery.F_MatchAnyPartOfName = null;
        retainerQuery.F_MatchAnyPartOfName = null;

        // Assert
        playerQuery.F_MatchAnyPartOfName.Should().BeNull();
        retainerQuery.F_MatchAnyPartOfName.Should().BeNull();
    }

    [Fact]
    public void QueryObjects_ShouldSupportPagination()
    {
        // Arrange
        var playerQuery = new PlayerQueryObject();
        var retainerQuery = new RetainerQueryObject();

        // Act
        playerQuery.Cursor = 100;
        retainerQuery.Cursor = 50;

        // Assert
        playerQuery.Cursor.Should().Be(100);
        retainerQuery.Cursor.Should().Be(50);
    }

    [Fact]
    public void QueryObjects_ShouldSupportFetchingState()
    {
        // Arrange
        var playerQuery = new PlayerQueryObject();
        var retainerQuery = new RetainerQueryObject();

        // Act
        playerQuery.IsFetching = true;
        retainerQuery.IsFetching = true;

        // Assert
        playerQuery.IsFetching.Should().BeTrue();
        retainerQuery.IsFetching.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void QueryObjects_F_MatchAnyPartOfName_DefaultBehavior(bool? setValue, bool expectedDefault)
    {
        // Arrange
        var playerQuery = new PlayerQueryObject();
        var retainerQuery = new RetainerQueryObject();

        if (setValue.HasValue)
        {
            // Act
            playerQuery.F_MatchAnyPartOfName = setValue;
            retainerQuery.F_MatchAnyPartOfName = setValue;

            // Assert
            playerQuery.F_MatchAnyPartOfName.Should().Be(setValue);
            retainerQuery.F_MatchAnyPartOfName.Should().Be(setValue);
        }
        else
        {
            // Test default values
            playerQuery.F_MatchAnyPartOfName.Should().Be(expectedDefault);
            retainerQuery.F_MatchAnyPartOfName.Should().Be(expectedDefault);
        }
    }

    #endregion

    #region Edge Cases and Validation

    [Fact]
    public void PlayerQueryObject_LargeWorldIdList_ShouldBeSupported()
    {
        // Arrange
        var query = new PlayerQueryObject();
        var largeWorldIdList = Enumerable.Range(1, 1000).Select(i => (short)i).ToList();

        // Act
        query.F_WorldIds.AddRange(largeWorldIdList);

        // Assert
        query.F_WorldIds.Should().HaveCount(1000);
        query.F_WorldIds.Should().BeEquivalentTo(largeWorldIdList);
    }

    [Fact]
    public void RetainerQueryObject_LargeWorldIdList_ShouldBeSupported()
    {
        // Arrange
        var query = new RetainerQueryObject();
        var largeWorldIdList = Enumerable.Range(1, 1000).Select(i => (short)i).ToList();

        // Act
        query.F_WorldIds.AddRange(largeWorldIdList);

        // Assert
        query.F_WorldIds.Should().HaveCount(1000);
        query.F_WorldIds.Should().BeEquivalentTo(largeWorldIdList);
    }

    [Fact]
    public void PlayerQueryObject_VeryLongName_ShouldBeSupported()
    {
        // Arrange
        var query = new PlayerQueryObject();
        var longName = new string('A', 1000); // Very long name

        // Act
        query.Name = longName;

        // Assert
        query.Name.Should().Be(longName);
        query.Name.Should().HaveLength(1000);
    }

    [Fact]
    public void RetainerQueryObject_VeryLongName_ShouldBeSupported()
    {
        // Arrange
        var query = new RetainerQueryObject();
        var longName = new string('B', 1000); // Very long name

        // Act
        query.Name = longName;

        // Assert
        query.Name.Should().Be(longName);
        query.Name.Should().HaveLength(1000);
    }

    [Fact]
    public void QueryObjects_ShouldHandleNegativeCursors()
    {
        // Arrange
        var playerQuery = new PlayerQueryObject();
        var retainerQuery = new RetainerQueryObject();

        // Act
        playerQuery.Cursor = -100;
        retainerQuery.Cursor = -50;

        // Assert
        playerQuery.Cursor.Should().Be(-100);
        retainerQuery.Cursor.Should().Be(-50);
    }

    [Fact]
    public void QueryObjects_ShouldHandleMaxIntValues()
    {
        // Arrange
        var playerQuery = new PlayerQueryObject();
        var retainerQuery = new RetainerQueryObject();

        // Act
        playerQuery.Cursor = int.MaxValue;
        retainerQuery.Cursor = int.MaxValue;

        // Assert
        playerQuery.Cursor.Should().Be(int.MaxValue);
        retainerQuery.Cursor.Should().Be(int.MaxValue);
    }

    #endregion
}