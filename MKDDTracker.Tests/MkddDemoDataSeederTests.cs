using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Models;
using MKDDTracker.Scraper.Parsing;
using MKDDTracker.Scraper.Services;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddDemoDataSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesFourClearlyBoundedDemoSnapshots()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDatabaseAsync(connection);

        var performances = await ParseSampleAsync();
        var seeder = new MkddDemoDataSeeder(db);

        await seeder.SeedAsync(
            "Luigi Circuit",
            "Mattilde F",
            "Enzo Vusur",
            performances);

        Assert.Equal(4, await db.Snapshots.CountAsync());
        Assert.Equal(performances.Count, await db.Players.CountAsync());
        Assert.Equal(
            performances.Count * 4,
            await db.Performances.CountAsync());

        var dates = await db.Snapshots
            .OrderBy(x => x.RankingDate)
            .Select(x => x.RankingDate)
            .ToListAsync();

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 9, 8),
                new DateOnly(2026, 9, 15),
                new DateOnly(2026, 9, 22)
            },
            dates);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = await CreateDatabaseAsync(connection);

        var performances = await ParseSampleAsync();
        var seeder = new MkddDemoDataSeeder(db);

        await seeder.SeedAsync(
            "Luigi Circuit",
            "Mattilde F",
            "Enzo Vusur",
            performances);
        await seeder.SeedAsync(
            "Luigi Circuit",
            "Mattilde F",
            "Enzo Vusur",
            performances);

        Assert.Equal(1, await db.Courses.CountAsync());
        Assert.Equal(performances.Count, await db.Players.CountAsync());
        Assert.Equal(4, await db.Snapshots.CountAsync());
        Assert.Equal(
            performances.Count * 4,
            await db.Performances.CountAsync());
    }

    private static async Task<MkddDbContext> CreateDatabaseAsync(
        SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new MkddDbContext(options);
        await db.Database.EnsureCreatedAsync();

        return db;
    }

    private static async Task<IReadOnlyList<MkddPerformance>>
        ParseSampleAsync()
    {
        var samplePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "luigi-circuit.html");
        var html = await File.ReadAllTextAsync(samplePath);

        return new MkddCoursePageParser().Parse(html);
    }
}
