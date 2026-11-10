using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Database.Migrations;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddDatabaseMigrationTests
{
    [Fact]
    public async Task InitializeAsync_MissingDatabase_CreatesMigratedDatabase()
    {
        var directory = DatabaseTestSupport.CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "mkddtracker.db");

        try
        {
            await using var db =
                DatabaseTestSupport.CreateContext(databasePath);
            var initializer = DatabaseTestSupport.CreateInitializer();

            await initializer.InitializeAsync(
                db,
                databasePath,
                DatabaseKind.User);

            Assert.True(File.Exists(databasePath));
            Assert.Empty(
                await db.Database.GetPendingMigrationsAsync());
            Assert.Empty(DatabaseTestSupport.GetBackups(databasePath));

            await using var connection =
                new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();

            foreach (var table in new[]
                     {
                         "Courses",
                         "Players",
                         "Snapshots",
                         "Performances",
                         "__EFMigrationsHistory"
                     })
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master " +
                    "WHERE type = 'table' AND name = $name;";
                command.Parameters.AddWithValue("$name", table);
                Assert.Equal(1L, Convert.ToInt64(
                    await command.ExecuteScalarAsync()));
            }

            await using var historyCommand = connection.CreateCommand();
            historyCommand.CommandText =
                "SELECT MigrationId FROM __EFMigrationsHistory;";
            Assert.Equal(
                MkddDatabaseMigrationMetadata.InitialCreateMigrationId,
                Convert.ToString(await historyCommand.ExecuteScalarAsync()));
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }
}
