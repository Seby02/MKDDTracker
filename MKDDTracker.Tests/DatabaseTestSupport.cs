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

    public static async Task CreateLegacyDatabaseAsync(
     string databasePath)
    {
        await using var connection =
            new SqliteConnection(
                $"Data Source={databasePath}");

        await connection.OpenAsync();

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
        PRAGMA foreign_keys = ON;

        CREATE TABLE "Courses" (
            "Id" INTEGER NOT NULL
                CONSTRAINT "PK_Courses"
                PRIMARY KEY AUTOINCREMENT,
            "Name" TEXT NOT NULL
        );

        CREATE TABLE "Players" (
            "Id" INTEGER NOT NULL
                CONSTRAINT "PK_Players"
                PRIMARY KEY AUTOINCREMENT,
            "SitePlayerId" INTEGER NOT NULL,
            "Name" TEXT NOT NULL,
            "Country" TEXT NULL
        );

        CREATE TABLE "Snapshots" (
            "Id" INTEGER NOT NULL
                CONSTRAINT "PK_Snapshots"
                PRIMARY KEY AUTOINCREMENT,
            "CourseId" INTEGER NOT NULL,
            "CapturedAt" TEXT NOT NULL,
            "RankingDate" TEXT NOT NULL,
            CONSTRAINT "FK_Snapshots_Courses_CourseId"
                FOREIGN KEY ("CourseId")
                REFERENCES "Courses" ("Id")
                ON DELETE CASCADE
        );

        CREATE TABLE "Performances" (
            "Id" INTEGER NOT NULL
                CONSTRAINT "PK_Performances"
                PRIMARY KEY AUTOINCREMENT,
            "SnapshotId" INTEGER NOT NULL,
            "PlayerId" INTEGER NOT NULL,
            "Rank" INTEGER NOT NULL,
            "Time" TEXT NOT NULL,
            "Standard" TEXT NULL,
            "RecordDate" TEXT NOT NULL,
            "VideoUrl" TEXT NULL,
            CONSTRAINT "FK_Performances_Players_PlayerId"
                FOREIGN KEY ("PlayerId")
                REFERENCES "Players" ("Id")
                ON DELETE CASCADE,
            CONSTRAINT "FK_Performances_Snapshots_SnapshotId"
                FOREIGN KEY ("SnapshotId")
                REFERENCES "Snapshots" ("Id")
                ON DELETE CASCADE
        );

        CREATE UNIQUE INDEX "IX_Courses_Name"
            ON "Courses" ("Name");

        CREATE UNIQUE INDEX "IX_Players_SitePlayerId"
            ON "Players" ("SitePlayerId");

        CREATE UNIQUE INDEX "IX_Snapshots_CourseId_RankingDate"
            ON "Snapshots" ("CourseId", "RankingDate");

        CREATE INDEX "IX_Performances_PlayerId"
            ON "Performances" ("PlayerId");

        CREATE UNIQUE INDEX "IX_Performances_SnapshotId_PlayerId"
            ON "Performances" ("SnapshotId", "PlayerId");

        INSERT INTO "Courses" (
            "Id",
            "Name"
        )
        VALUES (
            1,
            'Luigi Circuit'
        );

        INSERT INTO "Players" (
            "Id",
            "SitePlayerId",
            "Name",
            "Country"
        )
        VALUES (
            1,
            42,
            'Legacy Player',
            'FR'
        );

        INSERT INTO "Snapshots" (
            "Id",
            "CourseId",
            "CapturedAt",
            "RankingDate"
        )
        VALUES (
            1,
            1,
            '2026-09-15 12:30:00',
            '2026-09-15'
        );

        INSERT INTO "Performances" (
            "Id",
            "SnapshotId",
            "PlayerId",
            "Rank",
            "Time",
            "Standard",
            "RecordDate",
            "VideoUrl"
        )
        VALUES (
            1,
            1,
            1,
            1,
            '00:01:14.3210000',
            'God',
            '2026-09-14',
            'https://example.test/legacy'
        );
        """;

        await command.ExecuteNonQueryAsync();
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
