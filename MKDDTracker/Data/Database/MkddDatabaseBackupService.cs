using Microsoft.Data.Sqlite;

namespace MKDDTracker.Scraper.Data.Database;

public sealed class MkddDatabaseBackupService
{
    private readonly TimeProvider _timeProvider;

    public MkddDatabaseBackupService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<string> CreateBackupAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException(
                "The SQLite database to back up does not exist.",
                databasePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var backupPath = GetAvailableBackupPath(databasePath);
        var sourceConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();
        var destinationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        await using var source = new SqliteConnection(sourceConnectionString);
        await using var destination =
            new SqliteConnection(destinationConnectionString);

        try
        {
            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);

            source.BackupDatabase(destination);
            cancellationToken.ThrowIfCancellationRequested();

            return backupPath;
        }
        catch
        {
            await destination.CloseAsync();
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            throw;
        }
    }

    private string GetAvailableBackupPath(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath)
            ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileNameWithoutExtension(databasePath);
        var timestamp = _timeProvider.GetUtcNow()
            .ToString("yyyyMMdd-HHmmss");
        var candidate = Path.Combine(
            directory,
            $"{fileName}.backup-{timestamp}.db");

        for (var suffix = 1; File.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(
                directory,
                $"{fileName}.backup-{timestamp}-{suffix}.db");
        }

        return candidate;
    }
}
