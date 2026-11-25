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
    public async Task InitializeAsync_LegacyDatabase_BaselinesAndAppliesCurrentMigrations()
    {
        var directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        var databasePath =
            Path.Combine(
                directory,
                "mkddtracker.db");

        try
        {
            await DatabaseTestSupport.CreateLegacyDatabaseAsync(
                databasePath);

            await using var db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await DatabaseTestSupport
                .CreateInitializer()
                .InitializeAsync(
                    db,
                    databasePath,
                    DatabaseKind.User);

            await using var connection =
                new SqliteConnection(
                    $"Data Source={databasePath}");

            await connection.OpenAsync();

            //
            // Le schéma legacy doit avoir disparu.
            //

            Assert.False(
                await TableExistsAsync(
                    connection,
                    "Snapshots"));

            Assert.False(
                await TableExistsAsync(
                    connection,
                    "Performances"));

            //
            // Le schéma historique actuel doit exister.
            //

            Assert.True(
                await TableExistsAsync(
                    connection,
                    "Courses"));

            Assert.True(
                await TableExistsAsync(
                    connection,
                    "Players"));

            Assert.True(
                await TableExistsAsync(
                    connection,
                    "HistoricalPlayerAliases"));

            Assert.True(
                await TableExistsAsync(
                    connection,
                    "HistoricalPerformances"));

            Assert.True(
                await TableExistsAsync(
                    connection,
                    "HistoricalPerformanceSources"));

            //
            // Toutes les migrations doivent être appliquées.
            //

            Assert.Empty(
                await db.Database
                    .GetPendingMigrationsAsync());
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
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
            await using var connection =
    new SqliteConnection(
        $"Data Source={backupPath}");

            await connection.OpenAsync();

            Assert.Equal(
                1L,
                await CountRowsAsync(
                    connection,
                    "Courses"));

            Assert.Equal(
                1L,
                await CountRowsAsync(
                    connection,
                    "Players"));

            Assert.Equal(
                1L,
                await CountRowsAsync(
                    connection,
                    "Snapshots"));

            Assert.Equal(
                1L,
                await CountRowsAsync(
                    connection,
                    "Performances"));

            await using (var playerCommand =
                connection.CreateCommand())
            {
                playerCommand.CommandText =
                    """
        SELECT "Name"
        FROM "Players";
        """;

                Assert.Equal(
                    "Legacy Player",
                    Convert.ToString(
                        await playerCommand
                            .ExecuteScalarAsync()));
            }

            await using (var historyCommand =
                connection.CreateCommand())
            {
                historyCommand.CommandText =
                    """
        SELECT COUNT(*)
        FROM sqlite_master
        WHERE type = 'table'
          AND name = '__EFMigrationsHistory';
        """;

                Assert.Equal(
                    0L,
                    Convert.ToInt64(
                        await historyCommand
                            .ExecuteScalarAsync()));
            }
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

    private static async Task<long> CountRowsAsync(
    SqliteConnection connection,
    string tableName)
    {
        string quotedTableName =
            tableName.Replace(
                "\"",
                "\"\"",
                StringComparison.Ordinal);

        await using var command =
            connection.CreateCommand();

        command.CommandText =
            $"SELECT COUNT(*) FROM \"{quotedTableName}\";";

        return Convert.ToInt64(
            await command.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(
    SqliteConnection connection,
    string tableName)
    {
        await using var command =
            connection.CreateCommand();

        command.CommandText =
            """
        SELECT COUNT(*)
        FROM sqlite_master
        WHERE type = 'table'
          AND name = $tableName;
        """;

        command.Parameters.AddWithValue(
            "$tableName",
            tableName);

        return Convert.ToInt64(
            await command.ExecuteScalarAsync()) == 1L;
    }
}
