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
        var directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        var databasePath =
            Path.Combine(
                directory,
                "mkddtracker.db");

        try
        {
            await using var db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            var initializer =
                DatabaseTestSupport.CreateInitializer();

            await initializer.InitializeAsync(
                db,
                databasePath,
                DatabaseKind.User);

            Assert.True(
                File.Exists(databasePath));

            Assert.Empty(
                await db.Database
                    .GetPendingMigrationsAsync());

            Assert.Empty(
                DatabaseTestSupport.GetBackups(
                    databasePath));

            await using var connection =
                new SqliteConnection(
                    $"Data Source={databasePath}");

            await connection.OpenAsync();

            //
            // Tables du modèle actuel.
            //

            foreach (var table in new[]
            {
            "Courses",
            "Players",
            "HistoricalPlayerAliases",
            "HistoricalPerformances",
            "HistoricalPerformanceSources",
            "__EFMigrationsHistory"
        })
            {
                Assert.True(
                    await TableExistsAsync(
                        connection,
                        table),
                    $"La table '{table}' devrait exister.");
            }

            //
            // Les tables legacy ne doivent plus
            // exister après toutes les migrations.
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
            // Toutes les migrations doivent avoir
            // été appliquées.
            //

            await using var command =
                connection.CreateCommand();

            command.CommandText =
                """
            SELECT COUNT(*)
            FROM __EFMigrationsHistory;
            """;

            long appliedMigrationCount =
                Convert.ToInt64(
                    await command.ExecuteScalarAsync());

            Assert.True(
                appliedMigrationCount >= 3);

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
