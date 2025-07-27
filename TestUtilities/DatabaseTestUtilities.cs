using Microsoft.EntityFrameworkCore;

namespace TestUtilities;

public static class DatabaseTestUtilities
{
    public static DbContextOptions<T> CreateInMemoryDbOptions<T>(string? databaseName = null) where T : DbContext
    {
        databaseName ??= Guid.NewGuid().ToString();
        
        return new DbContextOptionsBuilder<T>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;
    }

    public static async Task<T> CreateCleanDatabaseAsync<T>(Func<DbContextOptions<T>, T> contextFactory, string? databaseName = null) where T : DbContext
    {
        var options = CreateInMemoryDbOptions<T>(databaseName);
        var context = contextFactory(options);
        
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        
        return context;
    }
}