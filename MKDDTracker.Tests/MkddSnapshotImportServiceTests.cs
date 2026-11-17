using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Services.SnapshotService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddSnapshotImportServiceTests
{
    private static readonly DateOnly RankingDate = new(2026, 9, 15);

    [Fact]
    public async Task ImportAsync_ValidHtml_CreatesCompleteSnapshot()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = await CreateDatabaseAsync(connection);
        var service = new MkddSnapshotImportService(db);

        var result = await service.ImportAsync(
            "Luigi Circuit",
            Html(Enumerable.Range(1, 100)
                .Select(id => Row(id, $"Player {id}", 70_000 + id))
                .ToArray()),
            RankingDate);

        Assert.Equal(SnapshotImportStatus.Created, result.Status);
        Assert.Equal(100, result.ImportedPerformances);
        Assert.NotNull(result.Snapshot);
        Assert.Null(result.Error);
        Assert.Equal(1, await db.Courses.CountAsync());
        Assert.Equal(1, await db.Snapshots.CountAsync());
        Assert.Equal(100, await db.Players.CountAsync());
        Assert.Equal(100, await db.Performances.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_SameSnapshotTwice_ReturnsAlreadyExistsWithoutDuplicates()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = await CreateDatabaseAsync(connection);
        var service = new MkddSnapshotImportService(db);
        var html = Html(Enumerable.Range(1, 100)
            .Select(id => Row(id, $"Player {id}", 70_000 + id))
            .ToArray());

        var first = await service.ImportAsync(
            "Luigi Circuit",
            html,
            RankingDate);
        var second = await service.ImportAsync(
            "Luigi Circuit",
            html,
            RankingDate);

        Assert.Equal(SnapshotImportStatus.Created, first.Status);
        Assert.Equal(SnapshotImportStatus.AlreadyExists, second.Status);
        Assert.Equal(0, second.ImportedPerformances);
        Assert.Equal(1, await db.Snapshots.CountAsync());
        Assert.Equal(100, await db.Performances.CountAsync());
        Assert.Equal(100, await db.Players.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_DuplicatePlayerInHtml_FailsBeforeAnyWrite()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = await CreateDatabaseAsync(connection);
        var service = new MkddSnapshotImportService(db);
        var html = Html(
            Row(42, "Alice", 74_000),
            Row(42, "Alice duplicate", 75_000));

        var result = await service.ImportAsync(
            "Luigi Circuit",
            html,
            RankingDate);

        Assert.Equal(SnapshotImportStatus.Failed, result.Status);
        Assert.Contains("plusieurs fois", result.Error);
        Assert.Equal(0, await db.Courses.CountAsync());
        Assert.Equal(0, await db.Snapshots.CountAsync());
        Assert.Equal(0, await db.Players.CountAsync());
        Assert.Equal(0, await db.Performances.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_NewAndExistingPlayers_ReusesExistingPlayer()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = await CreateDatabaseAsync(connection);

        db.Players.Add(new MkddPlayer
        {
            SitePlayerId = 42,
            Name = "Old Alice",
            Country = "Belgium"
        });
        await db.SaveChangesAsync();

        var service = new MkddSnapshotImportService(db);
        var result = await service.ImportAsync(
            "Luigi Circuit",
            Html(
                Row(42, "Alice", 74_000),
                Row(84, "Bob", 75_000)),
            RankingDate);

        Assert.Equal(SnapshotImportStatus.Created, result.Status);
        Assert.Equal(2, await db.Players.CountAsync());
        Assert.Equal(2, await db.Performances.CountAsync());

        var alice = await db.Players.SingleAsync(x => x.SitePlayerId == 42);
        Assert.Equal("Alice", alice.Name);
        Assert.Equal("France", alice.Country);
    }

    [Fact]
    public async Task ImportAsync_DatabaseFailure_RollsBackEveryEntity()
    {
        await using var connection = await OpenConnectionAsync();
        await using var db = await CreateDatabaseAsync(connection);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_performance_insert
            BEFORE INSERT ON Performances
            BEGIN
                SELECT RAISE(ABORT, 'forced performance failure');
            END;
            """);

        var service = new MkddSnapshotImportService(db);
        var result = await service.ImportAsync(
            "Luigi Circuit",
            Html(Row(42, "Alice", 74_000)),
            RankingDate);

        Assert.Equal(SnapshotImportStatus.Failed, result.Status);
        Assert.Contains("forced performance failure", result.Error);
        Assert.Equal(0, await db.Courses.CountAsync());
        Assert.Equal(0, await db.Snapshots.CountAsync());
        Assert.Equal(0, await db.Players.CountAsync());
        Assert.Equal(0, await db.Performances.CountAsync());
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
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

    private static string Html(params string[] rows) =>
        $"<html><body><table class=\"c\"><tbody>{string.Concat(rows)}</tbody></table></body></html>";

    private static string Row(
        int playerId,
        string playerName,
        int totalMilliseconds)
    {
        var minutes = totalMilliseconds / 60_000;
        var seconds = totalMilliseconds % 60_000 / 1_000;
        var milliseconds = totalMilliseconds % 1_000;
        var time = $"{minutes}'{seconds:00}\"{milliseconds:000}";

        return $"""
            <tr>
              <td>1</td>
              <td>{playerName}</td>
              <td><a href="profile.php?pid={playerId}">Profile</a></td>
              <td>France</td>
              <td>{time}</td>
              <td>God</td>
              <td>2026-09-15</td>
            </tr>
            """;
    }
}
