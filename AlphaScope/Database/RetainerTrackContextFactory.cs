#if EF
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AlphaScope.Database;

internal sealed class RetainerTrackContextFactory : IDesignTimeDbContextFactory<RetainerTrackContext>
{
    public RetainerTrackContext CreateDbContext(string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<RetainerTrackContext>().UseSqlite(
                $"Data Source={Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "pluginConfigs", "AlphaScope", Plugin.DatabaseFileName)}");
        return new RetainerTrackContext(optionsBuilder.Options);
    }
}
#endif
