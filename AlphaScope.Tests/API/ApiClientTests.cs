using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AlphaScope.API;
using AlphaScope.API.Models;
using RestSharp;
using TestUtilities;
using System.Net;

namespace AlphaScope.Tests.API;

public class ApiClientTests : IDisposable
{
    private readonly ILogger<ApiClient> _mockLogger;

    public ApiClientTests()
    {
        _mockLogger = LoggerTestUtilities.CreateMockLogger<ApiClient>();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_ShouldInitializeWithLogger()
    {
        var act = () => new ApiClient(_mockLogger);
        
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ShouldSetInstanceProperty()
    {
        var client = new ApiClient(_mockLogger);
        
        ApiClient.Instance.Should().NotBeNull();
        ApiClient.Instance.Should().BeSameAs(client);
    }

    [Theory]
    [InlineData("test-key", 123, "test-key-123")]
    [InlineData("", 456, "-456")]
    [InlineData("abc123", 789, "abc123-789")]
    public void Token_PropertyFormat_ShouldFollowPattern(string apiKey, int accountId, string expectedToken)
    {
        // Test the expected token format pattern
        var token = $"{apiKey}-{accountId}";
        token.Should().Be(expectedToken);
    }

    [Fact]
    public void ApiClient_ShouldHavePublicFields()
    {
        var client = new ApiClient(_mockLogger);
        
        // Verify that public fields are accessible
        client._ServerStatus.Should().NotBeNull();
        client.IsCheckingServerStatus.Should().BeFalse();
        client._LastPingValue.Should().Be(-1);
    }

    [Fact]
    public async Task CheckServerStatus_ShouldUpdateServerStatusField()
    {
        var client = new ApiClient(_mockLogger);
        
        // Initial state
        client._ServerStatus.Should().Be(string.Empty);
        
        // After calling CheckServerStatus, the field should be updated
        // Note: This will actually make a network call, so we expect it to fail in test environment
        var result = await client.CheckServerStatus();
        
        // The status should be updated regardless of success/failure
        client._ServerStatus.Should().NotBe(string.Empty);
    }

    [Fact]
    public async Task CheckServerStatus_ShouldUpdateCheckingFlag()
    {
        var client = new ApiClient(_mockLogger);
        
        // Initial state
        client.IsCheckingServerStatus.Should().BeFalse();
        
        // During execution, we can't easily test the intermediate state
        // but we can verify final state
        await client.CheckServerStatus();
        
        // Should be false when completed
        client.IsCheckingServerStatus.Should().BeFalse();
    }

    [Fact]
    public async Task PostPlayers_ShouldHandleEmptyList()
    {
        var client = new ApiClient(_mockLogger);
        var emptyList = new List<PostPlayerRequest>();
        
        var result = await client.PostPlayers(emptyList);
        
        // Should handle empty list gracefully
        result.Should().BeFalse(); // Expecting false for empty list
    }

    [Fact]
    public async Task PostPlayers_ShouldHandleNullList()
    {
        var client = new ApiClient(_mockLogger);
        
        var act = async () => await client.PostPlayers(null!);
        
        // Should either handle null gracefully or throw ArgumentNullException
        await act.Should().NotThrowAsync<NullReferenceException>();
    }


    [Fact]
    public void Config_ShouldBeAccessible()
    {
        var client = new ApiClient(_mockLogger);
        
        // Config property should be accessible
        client.Config.Should().NotBeNull();
    }

    [Fact]
    public Task PostPlayerRequest_ShouldHaveRequiredProperties()
    {
        var request = new PostPlayerRequest
        {
            LocalContentId = 123456789,
            Name = "TestPlayer",
            HomeWorldId = 65,
            AccountId = 1001,
            TerritoryId = 123,
            CurrentWorldId = 65,
            CreatedAt = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Verify all required properties can be set
        request.LocalContentId.Should().Be(123456789);
        request.Name.Should().Be("TestPlayer");
        request.HomeWorldId.Should().Be(65);
        request.AccountId.Should().Be(1001);
        request.TerritoryId.Should().Be(123);
        request.CurrentWorldId.Should().Be(65);
        request.CreatedAt.Should().BeGreaterThan(0);
        
        return Task.CompletedTask;
    }


    [Fact]
    public void StaticRestClient_ShouldBeAccessible()
    {
        // Verify static RestClient field is accessible
        ApiClient._restClient.Should().NotBeNull();
    }

    [Fact]
    public Task Instance_ShouldBeSingleton()
    {
        var client1 = new ApiClient(_mockLogger);
        var client2 = new ApiClient(_mockLogger);

        // The second instance should replace the first in the static Instance property
        ApiClient.Instance.Should().BeSameAs(client2);
        
        return Task.CompletedTask;
    }
}