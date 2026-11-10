using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MKDDTracker.Scraper.Data.Database.Migrations;

namespace MKDDTracker.Scraper.Data.Database;

public class MkddDatabaseInitializer
{
    private readonly MkddDatabaseBackupService _backupService;
    private readonly MkddDatabaseSchemaInspector _schemaInspector;
    private readonly ILogger<MkddDatabaseInitializer>? _logger;

    public MkddDatabaseInitializer(
        MkddDatabaseBackupService backupService,
        MkddDatabaseSchemaInspector? schemaInspector = null,
        ILogger<MkddDatabaseInitializer>? logger = null)
    {
        _backupService = backupService;
        _schemaInspector = schemaInspector
            ?? new MkddDatabaseSchemaInspector();
        _logger = logger;
    }

    public async Task InitializeAsync(
        MkddDbContext db,
        string databasePath,
        DatabaseKind databaseKind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullDatabasePath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullDatabasePath)
            ?? Directory.GetCurrentDirectory());

        var databaseExists = await _schemaInspector.DatabaseExistsAsync(
            fullDatabasePath,
            cancellationToken);

        if (!databaseExists)
        {
            await ApplyMigrationsAsync(db, cancellationToken);
            await EnsureNoPendingMigrationsAsync(db, cancellationToken);
            _logger?.LogInformation(
                "Database created through EF Core migrations at {DatabasePath}.",
                fullDatabasePath);
            return;
        }

        bool hasMigrationHistory;
        bool hasExpectedSchema;

        try
        {
            hasMigrationHistory =
                await _schemaInspector.HasEfMigrationHistoryAsync(
                    db,
                    cancellationToken);
            hasExpectedSchema = await _schemaInspector.HasLegacySchemaAsync(
                db,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is DbException or InvalidOperationException)
        {
            throw CreateUnknownSchemaException(
                fullDatabasePath,
                exception);
        }

        if (!hasExpectedSchema)
        {
            throw CreateUnknownSchemaException(fullDatabasePath);
        }

        if (!hasMigrationHistory)
        {
            _logger?.LogInformation(
                "Legacy MKDDTracker database detected at {DatabasePath}.",
                fullDatabasePath);

            if (databaseKind == DatabaseKind.User)
            {
                var backupPath = await _backupService.CreateBackupAsync(
                    fullDatabasePath,
                    cancellationToken);
                _logger?.LogInformation(
                    "Database backup created at {BackupPath}.",
                    backupPath);
            }

            await RegisterInitialBaselineAsync(db, cancellationToken);
            _logger?.LogInformation(
                "Initial EF Core migration baseline registered.");
            await ApplyMigrationsAsync(db, cancellationToken);
            await EnsureNoPendingMigrationsAsync(db, cancellationToken);
            _logger?.LogInformation("Database is up to date.");
            return;
        }

        var pendingMigrations = (await db.Database
                .GetPendingMigrationsAsync(cancellationToken))
            .ToArray();

        if (pendingMigrations.Length == 0)
        {
            _logger?.LogInformation("Database is already up to date.");
            return;
        }

        if (databaseKind == DatabaseKind.User)
        {
            var backupPath = await _backupService.CreateBackupAsync(
                fullDatabasePath,
                cancellationToken);
            _logger?.LogInformation(
                "Database backup created at {BackupPath}.",
                backupPath);
        }

        await ApplyMigrationsAsync(db, cancellationToken);
        await EnsureNoPendingMigrationsAsync(db, cancellationToken);
        _logger?.LogInformation("Database is up to date.");
    }

    protected virtual Task ApplyMigrationsAsync(
        MkddDbContext db,
        CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);

    private static async Task RegisterInitialBaselineAsync(
        MkddDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var createCommand = connection.CreateCommand())
            {
                createCommand.Transaction = transaction;
                createCommand.CommandText =
                    "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (" +
                    "\"MigrationId\" TEXT NOT NULL " +
                    "CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                    "\"ProductVersion\" TEXT NOT NULL);";
                await createCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.Transaction = transaction;
                insertCommand.CommandText =
                    "INSERT OR IGNORE INTO \"__EFMigrationsHistory\" " +
                    "(\"MigrationId\", \"ProductVersion\") " +
                    "VALUES ($migrationId, $productVersion);";
                AddParameter(
                    insertCommand,
                    "$migrationId",
                    MkddDatabaseMigrationMetadata.InitialCreateMigrationId);
                AddParameter(
                    insertCommand,
                    "$productVersion",
                    MkddDatabaseMigrationMetadata.ProductVersion);
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureNoPendingMigrationsAsync(
        MkddDbContext db,
        CancellationToken cancellationToken)
    {
        var pendingMigrations = (await db.Database
                .GetPendingMigrationsAsync(cancellationToken))
            .ToArray();

        if (pendingMigrations.Length > 0)
        {
            throw new InvalidOperationException(
                "Database initialization completed with pending migrations: " +
                string.Join(", ", pendingMigrations));
        }
    }

    private static InvalidOperationException CreateUnknownSchemaException(
        string databasePath,
        Exception? innerException = null) =>
        new(
            $"The SQLite database '{databasePath}' does not match the " +
            "expected MKDDTracker PR4 schema. It was left unchanged.",
            innerException);

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
