using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Models;
using MKDDTracker.Scraper.Services.SnapshotService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddSnapshotServiceTests
{
    [Fact]
    public async Task SaveSnapshotAsync_NormalizesRanksWithTheCentralRule()
    {
        var options = new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var db = new MkddDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var performances = new[]
        {
            Performance(1, "Alice", 99, 74_000),
            Performance(2, "Bob", 98, 74_500),
            Performance(3, "Claire", 97, 74_500),
            Performance(4, "David", 96, 75_000)
        };

        var service = new MkddSnapshotService(db);
        var snapshot = await service.SaveSnapshotAsync(
            "Luigi Circuit",
            performances,
            new DateOnly(2026, 9, 15),
            new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc));

        var persistedRanks = (await db.Performances
        .Where(x => x.SnapshotId == snapshot.Id)
        .ToListAsync())
    .OrderBy(x => x.Time)
    .Select(x => x.Rank)
    .ToList();

        Assert.Equal(new[] { 1, 2, 2, 4 }, persistedRanks);
    }

    private static MkddPerformance Performance(
        int sitePlayerId,
        string playerName,
        int sourceRank,
        int milliseconds) =>
        new(
            sourceRank,
            sitePlayerId,
            playerName,
            "France",
            TimeSpan.FromMilliseconds(milliseconds),
            "God",
            new DateOnly(2026, 9, 15),
            null);
}
