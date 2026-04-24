using MemoriaServer.Services.Admin;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MemoriaServer.Tests.Services;

public class AdminOptionsTests
{
    [Fact]
    public void Bind_FromConfig_ParsesDiscordUserIds()
    {
        var dict = new Dictionary<string, string?>
        {
            ["Admin:DiscordUserIds:0"] = "12345",
            ["Admin:DiscordUserIds:1"] = "67890",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var options = new AdminOptions();
        config.GetSection(AdminOptions.SectionName).Bind(options);

        options.DiscordUserIds.Should().BeEquivalentTo(new[] { 12345L, 67890L });
    }

    [Fact]
    public void Default_EmptyList()
    {
        var options = new AdminOptions();
        options.DiscordUserIds.Should().BeEmpty();
    }
}
