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
        //
        // Création manuelle du schéma historique PR4.
        //
        // Ne pas utiliser EnsureCreatedAsync(), car il créerait
        // également les nouvelles tables Historical*.
        //

        await using (var connection =
                     new SqliteConnection(
                         $"Data Source={databasePath}"))
        {
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

            CREATE UNIQUE INDEX
                "IX_Snapshots_CourseId_RankingDate"
                ON "Snapshots" (
                    "CourseId",
                    "RankingDate"
                );

            CREATE INDEX "IX_Performances_PlayerId"
                ON "Performances" ("PlayerId");

            CREATE UNIQUE INDEX
                "IX_Performances_SnapshotId_PlayerId"
                ON "Performances" (
                    "SnapshotId",
                    "PlayerId"
                );
            """;

            await command.ExecuteNonQueryAsync();
        }

        //
        // Insertion des données legacy via EF Core.
        //
        // EF peut écrire dans les anciennes tables sans avoir
        // besoin des nouvelles tables Historical*.
        //

        await using var db =
            CreateContext(databasePath);

        var course =
            new MkddCourse
            {
                Name = "Luigi Circuit"
            };

        var player =
            new MkddPlayer
            {
                SitePlayerId = 42,
                Name = "Legacy Player",
                Country = "FR"
            };

        var snapshot =
            new MkddSnapshot
            {
                Course = course,

                CapturedAt =
                    new DateTime(
                        2026,
                        9,
                        15,
                        12,
                        30,
                        0),

                RankingDate =
                    new DateOnly(
                        2026,
                        9,
                        15)
            };

        snapshot.Performances.Add(
            new MkddPerformanceEntity
            {
                Player = player,

                Rank = 1,

                Time =
                    TimeSpan.FromMilliseconds(
                        74_321),

                Standard = "God",

                RecordDate =
                    new DateOnly(
                        2026,
                        9,
                        14),

                VideoUrl =
                    "https://example.test/legacy"
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
