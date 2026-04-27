using FluentAssertions;
using MemoriaServer.Models.Entities;
using Xunit;

namespace MemoriaServer.Tests.Models;

public class PlayerEntityPhase1Tests
{
    [Fact]
    public void Player_has_OnlineStatusId_property()
    {
        var p = new Player { LocalContentId = 1, Name = "X" };
        p.OnlineStatusId = 27;
        p.OnlineStatusId.Should().Be(27);
    }

    [Fact]
    public void Player_OnlineStatusId_defaults_to_null()
    {
        new Player { LocalContentId = 1, Name = "X" }.OnlineStatusId.Should().BeNull();
    }

    [Fact]
    public void Player_has_TitleId_property_as_int()
    {
        var p = new Player { LocalContentId = 1, Name = "X" };
        p.TitleId = 65000; // ushort range — must not truncate
        p.TitleId.Should().Be(65000);
    }

    [Fact]
    public void Player_has_GrandCompanyId_property_as_byte()
    {
        var p = new Player { LocalContentId = 1, Name = "X" };
        p.GrandCompanyId = 3;
        p.GrandCompanyId.Should().Be(3);
    }

    [Fact]
    public void Player_has_FreeCompanyTag_property()
    {
        var p = new Player { LocalContentId = 1, Name = "X" };
        p.FreeCompanyTag = "Memo";
        p.FreeCompanyTag.Should().Be("Memo");
    }

    [Fact]
    public void Player_has_CurrentMountId_property_as_int()
    {
        var p = new Player { LocalContentId = 1, Name = "X" };
        p.CurrentMountId = 45;
        p.CurrentMountId.Should().Be(45);
    }

    [Fact]
    public void Player_has_CurrentMinionId_property_as_int()
    {
        var p = new Player { LocalContentId = 1, Name = "X" };
        p.CurrentMinionId = 200;
        p.CurrentMinionId.Should().Be(200);
    }

    [Fact]
    public void Player_has_LodestonePortraitUrl_property()
    {
        var p = new Player { LocalContentId = 1, Name = "X" };
        p.LodestonePortraitUrl = "https://img2.finalfantasyxiv.com/abc.jpg";
        p.LodestonePortraitUrl.Should().Be("https://img2.finalfantasyxiv.com/abc.jpg");
    }
}
