using Microsoft.Data.Sqlite;
using MKDDTracker.Scraper.Data.Database;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddDatabaseBackupServiceTests
{
    [Fact]
    public async Task CreateBackupAsync_WalDatabase_CopiesCommittedData()
    {
        var directory = DatabaseTestSupport.CreateTemporaryDirectory();
        var databasePath = Path.Combine(directory, "wal.db");

        try
        {
            await using var source =
                new SqliteConnection($"Data Source={databasePath}");
            await source.OpenAsync();

            foreach (var sql in new[]
                     {
                         "PRAGMA journal_mode=WAL;",
                         "CREATE TABLE Items " +
                         "(Id INTEGER PRIMARY KEY, Name TEXT);",
                         "INSERT INTO Items (Name) " +
                         "VALUES ('committed in WAL');"
                     })
            {
                await using var command = source.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }

            var service = new MkddDatabaseBackupService(
                new FixedTimeProvider(
                    new DateTimeOffset(
                        2026,
                        11,
                        10,
                        10,
                        31,
                        56,
                        TimeSpan.Zero)));

            var backupPath = await service.CreateBackupAsync(databasePath);

            Assert.Equal(
                Path.Combine(directory, "wal.backup-20261110-103156.db"),
                backupPath);
            Assert.True(File.Exists(backupPath));

            await using var backup =
                new SqliteConnection($"Data Source={backupPath}");
            await backup.OpenAsync();
            await using var query = backup.CreateCommand();
            query.CommandText = "SELECT Name FROM Items WHERE Id = 1;";
            Assert.Equal(
                "committed in WAL",
                Convert.ToString(await query.ExecuteScalarAsync()));
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
