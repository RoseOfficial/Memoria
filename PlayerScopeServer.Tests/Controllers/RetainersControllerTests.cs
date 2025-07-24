using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PlayerScopeServer.Controllers;
using PlayerScopeServer.Data;
using PlayerScopeServer.Models.DTOs;
using PlayerScopeServer.Models.Entities;
using TestUtilities;

namespace PlayerScopeServer.Tests.Controllers;

public class RetainersControllerTests : IDisposable
{
    private readonly PlayerScopeDbContext _context;
    private readonly ILogger<RetainersController> _mockLogger;
    private readonly RetainersController _controller;

    public RetainersControllerTests()
    {
        var options = DatabaseTestUtilities.CreateInMemoryDbOptions<PlayerScopeDbContext>();
        _context = new PlayerScopeDbContext(options);
        _context.Database.EnsureCreated();

        _mockLogger = LoggerTestUtilities.CreateMockLogger<RetainersController>();
        _controller = new RetainersController(_context, _mockLogger);

        // Setup HttpContext
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    public void Dispose()
    {
        _context?.Dispose();
    }

    [Fact]
    public async Task SearchRetainers_ShouldReturnAllRetainers_WhenNoFiltersApplied()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "Retainer1", WorldId = 65, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 2, Name = "Retainer2", WorldId = 66, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 3, Name = "Retainer3", WorldId = 67, OwnerLocalContentId = 1, Owner = owner }
        };

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchRetainers();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(3);
        pagination.Data.Should().Contain(r => r.Name == "Retainer1");
        pagination.Data.Should().Contain(r => r.Name == "Retainer2");
        pagination.Data.Should().Contain(r => r.Name == "Retainer3");
    }

    [Fact]
    public async Task SearchRetainers_ShouldFilterByExactName()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "TestRetainer", WorldId = 65, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 2, Name = "AnotherRetainer", WorldId = 66, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 3, Name = "TestRetainerExtra", WorldId = 67, OwnerLocalContentId = 1, Owner = owner }
        };

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchRetainers(Name: "TestRetainer", F_MatchAnyPartOfName: false);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(1);
        pagination.Data.First().Name.Should().Be("TestRetainer");
    }

    [Fact]
    public async Task SearchRetainers_ShouldFilterByPartialName()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "TestRetainer", WorldId = 65, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 2, Name = "AnotherRetainer", WorldId = 66, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 3, Name = "TestRetainerExtra", WorldId = 67, OwnerLocalContentId = 1, Owner = owner }
        };

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchRetainers(Name: "TestRetainer", F_MatchAnyPartOfName: true);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
        pagination.Data.Should().Contain(r => r.Name == "TestRetainer");
        pagination.Data.Should().Contain(r => r.Name == "TestRetainerExtra");
    }

    [Fact]
    public async Task SearchRetainers_ShouldFilterByWorldIds()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "Retainer1", WorldId = 65, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 2, Name = "Retainer2", WorldId = 66, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 3, Name = "Retainer3", WorldId = 67, OwnerLocalContentId = 1, Owner = owner }
        };

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchRetainers(F_WorldIds: "65,66");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
        pagination.Data.Should().Contain(r => r.Name == "Retainer1");
        pagination.Data.Should().Contain(r => r.Name == "Retainer2");
    }

    [Fact]
    public async Task SearchRetainers_ShouldIgnoreInvalidWorldIds()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "Retainer1", WorldId = 65, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 2, Name = "Retainer2", WorldId = 66, OwnerLocalContentId = 1, Owner = owner }
        };

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act - Include invalid world IDs
        var result = await _controller.SearchRetainers(F_WorldIds: "65,invalid,66,abc");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
        pagination.Data.Should().Contain(r => r.Name == "Retainer1");
        pagination.Data.Should().Contain(r => r.Name == "Retainer2");
    }

    [Fact]
    public async Task SearchRetainers_ShouldRespectPrivacyFilter_WhenUserNotOwner()
    {
        // Arrange
        var publicOwner = new Player { LocalContentId = 1, Name = "PublicOwner", CurrentWorldId = 65, IsPrivate = false, AccountId = 123 };
        var privateOwner = new Player { LocalContentId = 2, Name = "PrivateOwner", CurrentWorldId = 66, IsPrivate = true, AccountId = 456 };
        _context.Players.AddRange(publicOwner, privateOwner);

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "PublicRetainer", WorldId = 65, OwnerLocalContentId = 1, Owner = publicOwner },
            new() { LocalContentId = 2, Name = "PrivateRetainer", WorldId = 66, OwnerLocalContentId = 2, Owner = privateOwner }
        };

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Setup HttpContext without GameAccountId (anonymous user)
        _controller.ControllerContext.HttpContext.Items.Clear();

        // Act
        var result = await _controller.SearchRetainers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(1);
        pagination.Data.First().Name.Should().Be("PublicRetainer");
    }

    [Fact]
    public async Task SearchRetainers_ShouldShowPrivateRetainer_WhenUserIsOwner()
    {
        // Arrange
        var publicOwner = new Player { LocalContentId = 1, Name = "PublicOwner", CurrentWorldId = 65, IsPrivate = false, AccountId = 123 };
        var privateOwner = new Player { LocalContentId = 2, Name = "PrivateOwner", CurrentWorldId = 66, IsPrivate = true, AccountId = 456 };
        _context.Players.AddRange(publicOwner, privateOwner);

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "PublicRetainer", WorldId = 65, OwnerLocalContentId = 1, Owner = publicOwner },
            new() { LocalContentId = 2, Name = "PrivateRetainer", WorldId = 66, OwnerLocalContentId = 2, Owner = privateOwner }
        };

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Setup HttpContext with GameAccountId matching the private owner
        _controller.ControllerContext.HttpContext.Items["GameAccountId"] = 456;

        // Act
        var result = await _controller.SearchRetainers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
        pagination.Data.Should().Contain(r => r.Name == "PublicRetainer");
        pagination.Data.Should().Contain(r => r.Name == "PrivateRetainer");
    }

    [Fact]
    public async Task SearchRetainers_ShouldImplementCursorPagination()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainers = new List<Retainer>();
        for (int i = 1; i <= 30; i++)
        {
            retainers.Add(new Retainer 
            { 
                LocalContentId = i, 
                Name = $"Retainer{i}", 
                WorldId = 65, 
                OwnerLocalContentId = 1,
                Owner = owner
            });
        }

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act - First page
        var firstPageResult = await _controller.SearchRetainers(Cursor: 0);

        // Assert - First page should have 25 items (PageSize)
        var okResult = firstPageResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(25);
        pagination.LastCursor.Should().Be(25);
        pagination.NextCount.Should().Be(5); // Remaining 5 items

        // Act - Second page using cursor
        var secondPageResult = await _controller.SearchRetainers(Cursor: pagination.LastCursor + 1);

        // Assert - Second page should have remaining 5 items
        var okResult2 = secondPageResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination2 = okResult2.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination2.Data.Should().HaveCount(5);
        pagination2.NextCount.Should().Be(0); // No more items
    }

    [Fact]
    public async Task SearchRetainers_ShouldOrderByLocalContentId()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 3, Name = "Retainer3", WorldId = 65, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 1, Name = "Retainer1", WorldId = 66, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 2, Name = "Retainer2", WorldId = 67, OwnerLocalContentId = 1, Owner = owner }
        };

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchRetainers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(3);
        pagination.Data[0].LocalContentId.Should().Be(1);
        pagination.Data[1].LocalContentId.Should().Be(2);
        pagination.Data[2].LocalContentId.Should().Be(3);
    }

    [Fact]
    public async Task SearchRetainers_ShouldMapRetainerDataCorrectly()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainer = new Retainer
        {
            LocalContentId = 123456789,
            Name = "TestRetainer",
            WorldId = 65,
            OwnerLocalContentId = 1,
            Owner = owner,
            CreatedAt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        _context.Retainers.Add(retainer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchRetainers();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(1);
        var retainerDto = pagination.Data.First();
        
        retainerDto.LocalContentId.Should().Be(123456789);
        retainerDto.Name.Should().Be("TestRetainer");
        retainerDto.WorldId.Should().Be(65);
        retainerDto.OwnerLocalContentId.Should().Be(1);
        retainerDto.CreatedAt.Should().Be(1672574400); // Unix timestamp for 2023-01-01 12:00:00 UTC
    }

    [Fact]
    public async Task UploadRetainers_ShouldCreateNewRetainer_WhenRetainerDoesNotExist()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false, AccountId = 123 };
        _context.Players.Add(owner);
        await _context.SaveChangesAsync();

        _controller.ControllerContext.HttpContext.Items["GameAccountId"] = 123;

        var retainerRequest = new PostRetainerRequest
        {
            LocalContentId = 123456789,
            Name = "NewRetainer",
            WorldId = 65,
            OwnerLocalContentId = 1,
            CreatedAt = 1672574400 // 2023-01-01 12:00:00 UTC
        };

        // Act
        var result = await _controller.UploadRetainers(new List<PostRetainerRequest> { retainerRequest });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var dbRetainer = await _context.Retainers.FirstOrDefaultAsync(r => r.LocalContentId == 123456789);
        dbRetainer.Should().NotBeNull();
        dbRetainer!.Name.Should().Be("NewRetainer");
        dbRetainer.WorldId.Should().Be(65);
        dbRetainer.OwnerLocalContentId.Should().Be(1);
        
        // Verify history entries were created
        var nameHistory = await _context.RetainerNameHistory.FirstOrDefaultAsync(h => h.RetainerLocalContentId == 123456789);
        nameHistory.Should().NotBeNull();
        nameHistory!.Name.Should().Be("NewRetainer");
        
        var worldHistory = await _context.RetainerWorldHistory.FirstOrDefaultAsync(h => h.RetainerLocalContentId == 123456789);
        worldHistory.Should().NotBeNull();
        worldHistory!.WorldId.Should().Be(65);
    }

    [Fact]
    public async Task UploadRetainers_ShouldUpdateExistingRetainer_WhenNameChanges()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false, AccountId = 123 };
        _context.Players.Add(owner);

        var existingRetainer = new Retainer
        {
            LocalContentId = 123456789,
            Name = "OldName",
            WorldId = 65,
            OwnerLocalContentId = 1,
            Owner = owner
        };
        _context.Retainers.Add(existingRetainer);
        await _context.SaveChangesAsync();

        _controller.ControllerContext.HttpContext.Items["GameAccountId"] = 123;

        var retainerRequest = new PostRetainerRequest
        {
            LocalContentId = 123456789,
            Name = "NewName",
            WorldId = 65,
            OwnerLocalContentId = 1,
            CreatedAt = 1672574400
        };

        // Act
        var result = await _controller.UploadRetainers(new List<PostRetainerRequest> { retainerRequest });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var dbRetainer = await _context.Retainers.FirstOrDefaultAsync(r => r.LocalContentId == 123456789);
        dbRetainer.Should().NotBeNull();
        dbRetainer!.Name.Should().Be("NewName");
        
        // Verify name history was created
        var nameHistoryCount = await _context.RetainerNameHistory
            .CountAsync(h => h.RetainerLocalContentId == 123456789);
        nameHistoryCount.Should().Be(1);
    }

    [Fact]
    public async Task UploadRetainers_ShouldUpdateExistingRetainer_WhenWorldChanges()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false, AccountId = 123 };
        _context.Players.Add(owner);

        var existingRetainer = new Retainer
        {
            LocalContentId = 123456789,
            Name = "TestRetainer",
            WorldId = 65,
            OwnerLocalContentId = 1,
            Owner = owner
        };
        _context.Retainers.Add(existingRetainer);
        await _context.SaveChangesAsync();

        _controller.ControllerContext.HttpContext.Items["GameAccountId"] = 123;

        var retainerRequest = new PostRetainerRequest
        {
            LocalContentId = 123456789,
            Name = "TestRetainer",
            WorldId = 66, // Changed world
            OwnerLocalContentId = 1,
            CreatedAt = 1672574400
        };

        // Act
        var result = await _controller.UploadRetainers(new List<PostRetainerRequest> { retainerRequest });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var dbRetainer = await _context.Retainers.FirstOrDefaultAsync(r => r.LocalContentId == 123456789);
        dbRetainer.Should().NotBeNull();
        dbRetainer!.WorldId.Should().Be(66);
        
        // Verify world history was created
        var worldHistoryCount = await _context.RetainerWorldHistory
            .CountAsync(h => h.RetainerLocalContentId == 123456789);
        worldHistoryCount.Should().Be(1);
    }

    [Fact]
    public async Task UploadRetainers_ShouldSkipRetainer_WhenOwnerNotFound()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Items["GameAccountId"] = 123;

        var retainerRequest = new PostRetainerRequest
        {
            LocalContentId = 123456789,
            Name = "TestRetainer",
            WorldId = 65,
            OwnerLocalContentId = 999, // Non-existent owner
            CreatedAt = 1672574400
        };

        // Act
        var result = await _controller.UploadRetainers(new List<PostRetainerRequest> { retainerRequest });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var dbRetainer = await _context.Retainers.FirstOrDefaultAsync(r => r.LocalContentId == 123456789);
        dbRetainer.Should().BeNull();
    }

    [Fact]
    public async Task UploadRetainers_ShouldUpdateUserStats()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false, AccountId = 123 };
        var user = new ApplicationUser { GameAccountId = 123, UploadedRetainersCount = 5, UploadedRetainerInfoCount = 10 };
        _context.Players.Add(owner);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _controller.ControllerContext.HttpContext.Items["GameAccountId"] = 123;

        var retainerRequests = new List<PostRetainerRequest>
        {
            new() { LocalContentId = 1, Name = "Retainer1", WorldId = 65, OwnerLocalContentId = 1, CreatedAt = 1672574400 },
            new() { LocalContentId = 2, Name = "Retainer2", WorldId = 66, OwnerLocalContentId = 1, CreatedAt = 1672574400 }
        };

        // Act
        var result = await _controller.UploadRetainers(retainerRequests);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        
        var updatedUser = await _context.Users.FirstOrDefaultAsync(u => u.GameAccountId == 123);
        updatedUser.Should().NotBeNull();
        updatedUser!.UploadedRetainersCount.Should().Be(7); // 5 + 2
        updatedUser.UploadedRetainerInfoCount.Should().Be(12); // 10 + 2
    }

    [Fact]
    public async Task UploadRetainers_ShouldReturnUnauthorized_WhenGameAccountIdMissing()
    {
        // Arrange
        _controller.ControllerContext.HttpContext.Items.Clear();

        var retainerRequest = new PostRetainerRequest
        {
            LocalContentId = 123456789,
            Name = "TestRetainer",
            WorldId = 65,
            OwnerLocalContentId = 1,
            CreatedAt = 1672574400
        };

        // Act
        var result = await _controller.UploadRetainers(new List<PostRetainerRequest> { retainerRequest });

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SearchRetainers_ShouldHandleEmptyNameFilter(string emptyName)
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainers = new List<Retainer>
        {
            new() { LocalContentId = 1, Name = "Retainer1", WorldId = 65, OwnerLocalContentId = 1, Owner = owner },
            new() { LocalContentId = 2, Name = "Retainer2", WorldId = 66, OwnerLocalContentId = 1, Owner = owner }
        };

        _context.Retainers.AddRange(retainers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.SearchRetainers(Name: emptyName);

        // Assert - Should return all retainers when name filter is empty
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchRetainers_ShouldReturnEmptyResult_WhenNoRetainersMatch()
    {
        // Arrange
        var owner = new Player { LocalContentId = 1, Name = "Owner1", CurrentWorldId = 65, IsPrivate = false };
        _context.Players.Add(owner);

        var retainer = new Retainer { LocalContentId = 1, Name = "Retainer1", WorldId = 65, OwnerLocalContentId = 1, Owner = owner };
        _context.Retainers.Add(retainer);
        await _context.SaveChangesAsync();

        // Act - Search for non-existent retainer
        var result = await _controller.SearchRetainers(Name: "NonExistentRetainer");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var pagination = okResult.Value.Should().BeOfType<PaginationBase<RetainerSearchDto>>().Subject;
        
        pagination.Data.Should().BeEmpty();
        pagination.LastCursor.Should().Be(0);
        pagination.NextCount.Should().Be(0);
    }
}