using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Database.Migrations;
using MKDDTracker.Scraper.Data.Entities;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddDatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_CurrentDatabase_IsIdempotentAndCreatesNoBackup()
    {
        var directory = DatabaseTestSupport.CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "mkddtracker.db");

        try
        {
            var initializer = DatabaseTestSupport.CreateInitializer();

            await using (var first =
                         DatabaseTestSupport.CreateContext(databasePath))
            {
                await initializer.InitializeAsync(
                    first,
                    databasePath,
                    DatabaseKind.User);
                first.Courses.Add(new MkddCourse { Name = "Rainbow Road" });
                await first.SaveChangesAsync();
            }

            await using (var second =
                         DatabaseTestSupport.CreateContext(databasePath))
            {
                await initializer.InitializeAsync(
                    second,
                    databasePath,
                    DatabaseKind.User);
                Assert.Equal(
                    "Rainbow Road",
                    await second.Courses.Select(x => x.Name).SingleAsync());
            }

            Assert.Empty(DatabaseTestSupport.GetBackups(databasePath));
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task InitializeAsync_LegacyDatabase_BaselinesAndPreservesData()
    {
        var directory = DatabaseTestSupport.CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "mkddtracker.db");

        try
        {
            await DatabaseTestSupport.CreateLegacyDatabaseAsync(databasePath);

            await using var db =
                DatabaseTestSupport.CreateContext(databasePath);
            await DatabaseTestSupport.CreateInitializer().InitializeAsync(
                db,
                databasePath,
                DatabaseKind.User);

            var performance = await db.Performances
                .Include(x => x.Player)
                .Include(x => x.Snapshot)
                .ThenInclude(x => x.Course)
                .SingleAsync();

            Assert.Equal("Luigi Circuit", performance.Snapshot.Course.Name);
            Assert.Equal(new DateOnly(2026, 9, 15),
                performance.Snapshot.RankingDate);
            Assert.Equal("Legacy Player", performance.Player.Name);
            Assert.Equal(42, performance.Player.SitePlayerId);
            Assert.Equal("FR", performance.Player.Country);
            Assert.Equal(1, performance.Rank);
            Assert.Equal(TimeSpan.FromMilliseconds(74_321), performance.Time);
            Assert.Equal("God", performance.Standard);
            Assert.Equal(
                new DateOnly(2026, 9, 14),
                performance.RecordDate);
            Assert.Equal(
                "https://example.test/legacy",
                performance.VideoUrl);

            await using var connection =
                new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT MigrationId FROM __EFMigrationsHistory;";
            Assert.Equal(
                MkddDatabaseMigrationMetadata.InitialCreateMigrationId,
                Convert.ToString(await command.ExecuteScalarAsync()));
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task InitializeAsync_LegacyUserDatabase_CreatesReadablePreBaselineBackup()
    {
        var directory = DatabaseTestSupport.CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "mkddtracker.db");

        try
        {
            await DatabaseTestSupport.CreateLegacyDatabaseAsync(databasePath);

            await using (var db =
                         DatabaseTestSupport.CreateContext(databasePath))
            {
                await DatabaseTestSupport.CreateInitializer().InitializeAsync(
                    db,
                    databasePath,
                    DatabaseKind.User);
            }

            var backupPath = Assert.Single(
                DatabaseTestSupport.GetBackups(databasePath));
            await using var backup =
                DatabaseTestSupport.CreateContext(backupPath);

            Assert.Equal(1, await backup.Courses.CountAsync());
            Assert.Equal(1, await backup.Players.CountAsync());
            Assert.Equal(1, await backup.Snapshots.CountAsync());
            Assert.Equal(1, await backup.Performances.CountAsync());
            Assert.Equal(
                "Legacy Player",
                await backup.Players.Select(x => x.Name).SingleAsync());

            await using var connection =
                new SqliteConnection($"Data Source={backupPath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master " +
                "WHERE type = 'table' " +
                "AND name = '__EFMigrationsHistory';";
            Assert.Equal(
                0L,
                Convert.ToInt64(await command.ExecuteScalarAsync()));
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task InitializeAsync_UnknownSchema_RefusesWithoutDeletingDatabase()
    {
        var directory = DatabaseTestSupport.CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "unknown.db");

        try
        {
            await using (var connection =
                         new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "CREATE TABLE Unexpected (Id INTEGER PRIMARY KEY);";
                await command.ExecuteNonQueryAsync();
            }

            await using var db =
                DatabaseTestSupport.CreateContext(databasePath);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => DatabaseTestSupport.CreateInitializer().InitializeAsync(
                    db,
                    databasePath,
                    DatabaseKind.User));

            Assert.Contains("expected MKDDTracker PR4 schema", exception.Message);
            Assert.True(File.Exists(databasePath));
            Assert.Empty(DatabaseTestSupport.GetBackups(databasePath));

            await using var check =
                new SqliteConnection($"Data Source={databasePath}");
            await check.OpenAsync();
            await using var query = check.CreateCommand();
            query.CommandText =
                "SELECT COUNT(*) FROM sqlite_master " +
                "WHERE type = 'table' AND name = 'Unexpected';";
            Assert.Equal(
                1L,
                Convert.ToInt64(await query.ExecuteScalarAsync()));
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task InitializeAsync_MigrationFailure_KeepsBackupAvailable()
    {
        var directory = DatabaseTestSupport.CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "mkddtracker.db");

        try
        {
            await DatabaseTestSupport.CreateLegacyDatabaseAsync(databasePath);
            await using var db =
                DatabaseTestSupport.CreateContext(databasePath);
            var initializer = new FailingDatabaseInitializer(
                new MkddDatabaseBackupService());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => initializer.InitializeAsync(
                    db,
                    databasePath,
                    DatabaseKind.User));

            Assert.Equal("Forced migration failure.", exception.Message);
            var backupPath = Assert.Single(
                DatabaseTestSupport.GetBackups(databasePath));
            await using var backup =
                DatabaseTestSupport.CreateContext(backupPath);
            Assert.Equal(
                "Legacy Player",
                await backup.Players.Select(x => x.Name).SingleAsync());
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task DemoReset_DeletesSidecarsAndRecreatesMigratedDatabaseWithoutBackup()
    {
        var directory = DatabaseTestSupport.CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "mkddtracker-demo.db");

        try
        {
            var initializer = DatabaseTestSupport.CreateInitializer();

            await using (var first =
                         DatabaseTestSupport.CreateContext(databasePath))
            {
                await initializer.InitializeAsync(
                    first,
                    databasePath,
                    DatabaseKind.Demo);

                first.Courses.Add(
                    new MkddCourse { Name = "Demo Course" });

                await first.SaveChangesAsync();
            }

            // Important sur Windows :
            // libère les connexions SQLite conservées dans le pool.
            SqliteConnection.ClearAllPools();

            File.WriteAllText(
                $"{databasePath}-wal",
                "stale WAL");

            File.WriteAllText(
                $"{databasePath}-shm",
                "stale SHM");

            MkddDatabaseFiles.Delete(databasePath);

            Assert.False(File.Exists(databasePath));
            Assert.False(File.Exists($"{databasePath}-wal"));
            Assert.False(File.Exists($"{databasePath}-shm"));

            await using (var recreated =
                         DatabaseTestSupport.CreateContext(databasePath))
            {
                await initializer.InitializeAsync(
                    recreated,
                    databasePath,
                    DatabaseKind.Demo);

                Assert.Empty(
                    await recreated.Courses.ToListAsync());

                Assert.Empty(
                    await recreated.Database
                        .GetPendingMigrationsAsync());
            }

            Assert.Empty(
                DatabaseTestSupport.GetBackups(databasePath));
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(directory);
        }
    }

    private sealed class FailingDatabaseInitializer
        : MkddDatabaseInitializer
    {
        public FailingDatabaseInitializer(
            MkddDatabaseBackupService backupService)
            : base(backupService)
        {
        }

        protected override Task ApplyMigrationsAsync(
            MkddDbContext db,
            CancellationToken cancellationToken) =>
            Task.FromException(
                new InvalidOperationException("Forced migration failure."));
    }
}
