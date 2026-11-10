using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace MKDDTracker.Scraper.Data.Database;

public sealed class MkddDatabaseSchemaInspector
{
    private static readonly IReadOnlyDictionary<string, string[]> LegacySchema =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Courses"] = ["Id", "Name"],
            ["Players"] = ["Id", "SitePlayerId", "Name", "Country"],
            ["Snapshots"] = ["Id", "CourseId", "CapturedAt", "RankingDate"],
            ["Performances"] =
            [
                "Id",
                "SnapshotId",
                "PlayerId",
                "Rank",
                "Time",
                "Standard",
                "RecordDate",
                "VideoUrl"
            ]
        };

    public Task<bool> DatabaseExistsAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(databasePath));
    }

    public Task<bool> HasEfMigrationHistoryAsync(
        MkddDbContext db,
        CancellationToken cancellationToken = default) =>
        TableExistsAsync(db, "__EFMigrationsHistory", cancellationToken);

    public async Task<bool> HasLegacySchemaAsync(
        MkddDbContext db,
        CancellationToken cancellationToken = default)
    {
        foreach (var table in LegacySchema)
        {
            if (!await TableExistsAsync(db, table.Key, cancellationToken))
            {
                return false;
            }

            var columns = await GetColumnsAsync(
                db,
                table.Key,
                cancellationToken);

            if (!table.Value.All(columns.Contains))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> TableExistsAsync(
        MkddDbContext db,
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master " +
                "WHERE type = 'table' AND name = $name;";
            AddParameter(command, "$name", tableName);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result) == 1;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        MkddDbContext db,
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

            var columns = new HashSet<string>(StringComparer.Ordinal);
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

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
