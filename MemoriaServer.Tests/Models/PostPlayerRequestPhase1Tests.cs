using FluentAssertions;
using MemoriaServer.Models.DTOs;
using Newtonsoft.Json;
using Xunit;

namespace MemoriaServer.Tests.Models;

public class PostPlayerRequestPhase1Tests
{
    [Fact]
    public void Round_trips_OnlineStatusId_under_key_21()
    {
        var json = "{\"21\":27}";
        var req = JsonConvert.DeserializeObject<PostPlayerRequest>(json)!;
        req.OnlineStatusId.Should().Be(27);
    }

    [Fact]
    public void Round_trips_TitleId_under_key_22()
    {
        var json = "{\"22\":1234}";
        var req = JsonConvert.DeserializeObject<PostPlayerRequest>(json)!;
        req.TitleId.Should().Be(1234);
    }

    [Fact]
    public void Round_trips_GrandCompanyId_under_key_23()
    {
        var json = "{\"23\":2}";
        var req = JsonConvert.DeserializeObject<PostPlayerRequest>(json)!;
        req.GrandCompanyId.Should().Be(2);
    }

    [Fact]
    public void Round_trips_FreeCompanyTag_under_key_24()
    {
        var json = "{\"24\":\"Memo\"}";
        var req = JsonConvert.DeserializeObject<PostPlayerRequest>(json)!;
        req.FreeCompanyTag.Should().Be("Memo");
    }

    [Fact]
    public void Round_trips_CurrentMountId_under_key_25()
    {
        var json = "{\"25\":45}";
        var req = JsonConvert.DeserializeObject<PostPlayerRequest>(json)!;
        req.CurrentMountId.Should().Be(45);
    }

    [Fact]
    public void Round_trips_CurrentMinionId_under_key_26()
    {
        var json = "{\"26\":200}";
        var req = JsonConvert.DeserializeObject<PostPlayerRequest>(json)!;
        req.CurrentMinionId.Should().Be(200);
    }

    [Fact]
    public void FreeCompanyTag_has_MaxLength_7_attribute()
    {
        // Mirrors Player.FreeCompanyTag's [MaxLength(7)]. Without this on the
        // DTO, oversize tags skip ASP.NET model validation and throw
        // DbUpdateException at SaveChanges (500), instead of returning 400.
        var prop = typeof(MemoriaServer.Models.DTOs.PostPlayerRequest).GetProperty(nameof(MemoriaServer.Models.DTOs.PostPlayerRequest.FreeCompanyTag))!;
        var attr = prop.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.MaxLengthAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.MaxLengthAttribute>()
            .FirstOrDefault();
        attr.Should().NotBeNull();
        attr!.Length.Should().Be(7);
    }

    [Fact]
    public void All_phase_1_fields_default_to_null_when_absent()
    {
        var json = "{\"1\":1,\"2\":\"X\"}";
        var req = JsonConvert.DeserializeObject<PostPlayerRequest>(json)!;
        req.OnlineStatusId.Should().BeNull();
        req.TitleId.Should().BeNull();
        req.GrandCompanyId.Should().BeNull();
        req.FreeCompanyTag.Should().BeNull();
        req.CurrentMountId.Should().BeNull();
        req.CurrentMinionId.Should().BeNull();
    }
}
