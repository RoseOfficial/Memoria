using MemoriaServer.Data;
using MemoriaServer.Models.Entities;
using FluentAssertions;
using TestUtilities;
using Xunit;

namespace MemoriaServer.Tests.Data;

public class TakedownRequestSchemaTests
{
    [Fact]
    public void TakedownRequests_DbSetExists()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.TakedownRequests.Should().NotBeNull();
    }

    [Fact]
    public async Task TakedownRequest_CanInsertAndRoundTrip()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        ctx.TakedownRequests.Add(new TakedownRequest
        {
            WorldSlug = "balmung",
            NameSlug = "tataru-taru",
            Reason = "test",
            ContactEmail = "a@b.com",
            SubmitterIpHash = "abc",
        });
        await ctx.SaveChangesAsync();

        var fetched = ctx.TakedownRequests.First();
        fetched.Status.Should().Be(TakedownStatus.Pending);
        fetched.SubmittedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void TakedownRequest_Indexes_AreConfigured()
    {
        using var ctx = new MemoriaDbContext(DatabaseTestUtilities.CreateInMemoryDbOptions<MemoriaDbContext>());
        var entity = ctx.Model.FindEntityType(typeof(TakedownRequest))!;
        var indexes = entity.GetIndexes().Select(i => string.Join(",", i.Properties.Select(p => p.Name))).ToList();

        indexes.Should().Contain("Status");
        indexes.Should().Contain("WorldSlug,NameSlug");
        indexes.Should().Contain("SubmittedAt");
        indexes.Should().Contain("SubmitterIpHash");
    }
}
