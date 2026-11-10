using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using Microsoft.Data.Sqlite;

namespace MKDDTracker.Tests;

internal static class DatabaseTestSupport
{
    public static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "MKDDTracker.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static MkddDbContext CreateContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new MkddDbContext(options);
    }

    public static MkddDatabaseInitializer CreateInitializer(
        TimeProvider? timeProvider = null) =>
        new(new MkddDatabaseBackupService(timeProvider));

    public static async Task CreateLegacyDatabaseAsync(string databasePath)
    {
        await using var db = CreateContext(databasePath);
        await db.Database.EnsureCreatedAsync();

        var course = new MkddCourse { Name = "Luigi Circuit" };
        var player = new MkddPlayer
        {
            SitePlayerId = 42,
            Name = "Legacy Player",
            Country = "FR"
        };
        var snapshot = new MkddSnapshot
        {
            Course = course,
            CapturedAt = new DateTime(2026, 9, 15, 12, 30, 0),
            RankingDate = new DateOnly(2026, 9, 15)
        };
        snapshot.Performances.Add(
            new MkddPerformanceEntity
            {
                Player = player,
                Rank = 1,
                Time = TimeSpan.FromMilliseconds(74_321),
                Standard = "God",
                RecordDate = new DateOnly(2026, 9, 14),
                VideoUrl = "https://example.test/legacy"
            });

        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync();
    }

    public static string[] GetBackups(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath)!;
        var stem = Path.GetFileNameWithoutExtension(databasePath);
        return Directory.GetFiles(
            directory,
            $"{stem}.backup-*.db",
            SearchOption.TopDirectoryOnly);
    }

    public static async Task DeleteDirectoryAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        SqliteConnection.ClearAllPools();

        const int maxAttempts = 10;
        const int delayMilliseconds = 100;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                SqliteConnection.ClearAllPools();
                await Task.Delay(delayMilliseconds);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                SqliteConnection.ClearAllPools();
                await Task.Delay(delayMilliseconds);
            }
        }

        Directory.Delete(directory, recursive: true);
    }
}
