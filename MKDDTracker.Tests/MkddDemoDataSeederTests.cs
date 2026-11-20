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
    public async Task SeedAsync_CreatesHistoricalDemoEventsWithoutSnapshots()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        await connection.OpenAsync();

        await using MkddDbContext db =
            await CreateDatabaseAsync(
                connection);

        IReadOnlyList<MkddPerformance> performances =
            await ParseSampleAsync();

        var seeder =
            new MkddDemoDataSeeder(db);

        await seeder.SeedAsync(
            "Luigi Circuit",
            "Mattilde F",
            "Enzo Vusur",
            performances);

        Assert.Equal(
            0,
            await db.Snapshots.CountAsync());

        Assert.Equal(
            0,
            await db.Performances.CountAsync());

        Assert.Equal(
            performances.Count,
            await db.Players.CountAsync());

        Assert.Equal(
            performances.Count,
            await db.HistoricalPlayerAliases
                .CountAsync(alias =>
                    alias.SourceSystem ==
                        "MKDDTrackerDemo"));

        int historicalPerformanceCount =
            await db.HistoricalPerformances
                .CountAsync(performance =>
                    performance.SourceSystem ==
                        "MKDDTrackerDemo");

        Assert.True(
            historicalPerformanceCount >=
                performances.Count);

        Assert.True(
            historicalPerformanceCount <
                performances.Count * 4);

        Assert.All(
            await db.HistoricalPerformances
                .Where(performance =>
                    performance.SourceSystem ==
                        "MKDDTrackerDemo")
                .ToListAsync(),
            performance =>
            {
                Assert.Equal(
                    "ThreeLap",
                    performance.TimeType);

                Assert.Null(
                    performance.Frequency);

                Assert.Equal(
                    "DemoSeed",
                    performance.DateSource);

                Assert.Equal(
                    "Approved",
                    performance.ImportStatus);
            });
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        await connection.OpenAsync();

        await using MkddDbContext db =
            await CreateDatabaseAsync(
                connection);

        IReadOnlyList<MkddPerformance> performances =
            await ParseSampleAsync();

        var seeder =
            new MkddDemoDataSeeder(db);

        await seeder.SeedAsync(
            "Luigi Circuit",
            "Mattilde F",
            "Enzo Vusur",
            performances);

        int aliasesAfterFirstSeed =
            await db.HistoricalPlayerAliases
                .CountAsync(alias =>
                    alias.SourceSystem ==
                        "MKDDTrackerDemo");

        int performancesAfterFirstSeed =
            await db.HistoricalPerformances
                .CountAsync(performance =>
                    performance.SourceSystem ==
                        "MKDDTrackerDemo");

        await seeder.SeedAsync(
            "Luigi Circuit",
            "Mattilde F",
            "Enzo Vusur",
            performances);

        Assert.Equal(
            aliasesAfterFirstSeed,
            await db.HistoricalPlayerAliases
                .CountAsync(alias =>
                    alias.SourceSystem ==
                        "MKDDTrackerDemo"));

        Assert.Equal(
            performancesAfterFirstSeed,
            await db.HistoricalPerformances
                .CountAsync(performance =>
                    performance.SourceSystem ==
                        "MKDDTrackerDemo"));

        Assert.Equal(
            performancesAfterFirstSeed,
            await db.HistoricalPerformances
                .Where(performance =>
                    performance.SourceSystem ==
                        "MKDDTrackerDemo")
                .Select(performance =>
                    performance.ExternalKey)
                .Distinct()
                .CountAsync());
    }

    [Fact]
    public async Task SeedAsync_CreatesOrderedImprovementsForMattilde()
    {
        await using var connection =
            new SqliteConnection(
                "Data Source=:memory:");

        await connection.OpenAsync();

        await using MkddDbContext db =
            await CreateDatabaseAsync(
                connection);

        IReadOnlyList<MkddPerformance> performances =
            await ParseSampleAsync();

        var seeder =
            new MkddDemoDataSeeder(db);

        await seeder.SeedAsync(
            "Luigi Circuit",
            "Mattilde F",
            "Enzo Vusur",
            performances);

        var mattildeEvents =
            await db.HistoricalPerformances
                .AsNoTracking()
                .Where(performance =>
                    performance.SourceSystem ==
                        "MKDDTrackerDemo"
                    &&
                    performance
                        .HistoricalPlayerAlias
                        .Player != null
                    &&
                    performance
                        .HistoricalPlayerAlias
                        .Player!
                        .Name ==
                        "Mattilde F")
                .OrderBy(performance =>
                    performance.SequenceNumber)
                .ToListAsync();

        Assert.NotEmpty(
            mattildeEvents);

        Assert.Equal(
            1,
            mattildeEvents[0]
                .SequenceNumber);

        Assert.Null(
            mattildeEvents[0]
                .PreviousTime);

        Assert.Null(
            mattildeEvents[0]
                .Improvement);

        for (var index = 1;
             index < mattildeEvents.Count;
             index++)
        {
            Assert.True(
                mattildeEvents[index].Time <
                mattildeEvents[index - 1].Time);

            Assert.Equal(
                mattildeEvents[index - 1].Time,
                mattildeEvents[index]
                    .PreviousTime);

            Assert.Equal(
                mattildeEvents[index - 1].Time -
                    mattildeEvents[index].Time,
                mattildeEvents[index]
                    .Improvement);

            Assert.True(
                mattildeEvents[index]
                    .Improvement >
                TimeSpan.Zero);
        }
    }

    private static async Task<MkddDbContext>
        CreateDatabaseAsync(
            SqliteConnection connection)
    {
        var options =
            new DbContextOptionsBuilder<
                    MkddDbContext>()
                .UseSqlite(connection)
                .Options;

        var db =
            new MkddDbContext(
                options);

        await db.Database
            .EnsureCreatedAsync();

        return db;
    }

    private static async Task<
        IReadOnlyList<MkddPerformance>>
        ParseSampleAsync()
    {
        string samplePath =
            Path.Combine(
                AppContext.BaseDirectory,
                "TestData",
                "luigi-circuit.html");

        string html =
            await File.ReadAllTextAsync(
                samplePath);

        return new MkddCoursePageParser()
            .Parse(html);
    }
}