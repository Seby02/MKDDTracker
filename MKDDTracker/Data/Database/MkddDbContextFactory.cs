using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MKDDTracker.Scraper.Data.Database;

public sealed class MkddDbContextFactory
    : IDesignTimeDbContextFactory<MkddDbContext>
{
    public MkddDbContext CreateDbContext(string[] args)
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            "mkddtracker-design.db");

        var options = new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new MkddDbContext(options);
    }
}
