using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;

namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class MkddHistoricalImportDryRunService
{
    private readonly MkddDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    private static readonly IReadOnlyDictionary<string, string>
        CourseNamesByCode =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["LC"] = "Luigi Circuit",
                ["PB"] = "Peach Beach",
                ["BP"] = "Baby Park",
                ["DDD"] = "Dry Dry Desert",

                ["MB"] = "Mushroom Bridge",
                ["MaC"] = "Mario Circuit",
                ["DC"] = "Daisy Cruiser",
                ["WS"] = "Waluigi Stadium",

                ["SL"] = "Sherbet Land",
                ["MuC"] = "Mushroom City",
                ["YC"] = "Yoshi Circuit",
                ["DKM"] = "DK Mountain",

                ["WC"] = "Wario Colosseum",
                ["DDJ"] = "Dino Dino Jungle",
                ["BC"] = "Bowser's Castle",
                ["RR"] = "Rainbow Road"
            };

    public MkddHistoricalImportDryRunService(
        MkddDbContext db)
    {
        _db = db;
    }

    public async Task<HistoricalImportDryRunResult> AnalyzeAsync(
        string jsonFilePath,
        CancellationToken cancellationToken = default)
    {
        var result =
            new HistoricalImportDryRunResult();

        if (!File.Exists(jsonFilePath))
        {
            result.Errors.Add(
                $"Fichier introuvable : {jsonFilePath}");

            return result;
        }

        List<YahooHistoricalRecord> records =
            await ReadRecordsAsync(
                jsonFilePath,
                cancellationToken);

        result.InputRecordsCount =
            records.Count;

        List<YahooHistoricalRecord> validRecords =
            records
                .Where(IsValidRecord)
                .ToList();

        result.InvalidRecordsCount =
            records.Count -
            validRecords.Count;

        //
        // Circuits existants et circuits manquants.
        //

        List<MkddCourse> existingCourses =
            await _db.Courses
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        HashSet<string> existingCourseNames =
            existingCourses
                .Select(course =>
                    NormalizeText(course.Name))
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        List<string> requiredCourseNames =
            validRecords
                .Select(record =>
                    GetCourseName(
                        record.TrackCode))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

        result.CoursesAlreadyExistingCount =
            requiredCourseNames.Count(name =>
                existingCourseNames.Contains(
                    NormalizeText(name)));

        result.CoursesToCreate =
            requiredCourseNames
                .Where(name =>
                    !existingCourseNames.Contains(
                        NormalizeText(name)))
                .ToList();

        result.CoursesToCreateCount =
            result.CoursesToCreate.Count;

        //
        // Joueurs modernes existants.
        //

        List<MkddPlayer> players =
            await _db.Players
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        Dictionary<string, List<MkddPlayer>>
            playersByNormalizedName =
                players
                    .GroupBy(
                        player =>
                            NormalizePlayerName(
                                player.Name),
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToList(),
                        StringComparer.OrdinalIgnoreCase);

        List<PlayerAliasPlan> aliasPlans =
            validRecords
                .GroupBy(
                    record =>
                        NormalizePlayerName(
                            record.PlayerName),
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    playersByNormalizedName.TryGetValue(
                        group.Key,
                        out List<MkddPlayer>? candidates);

                    candidates ??= [];

                    return new PlayerAliasPlan
                    {
                        ExternalName =
                            group.First().PlayerName,

                        NormalizedExternalName =
                            group.Key,

                        PlayerId =
                            candidates.Count == 1
                                ? candidates[0].Id
                                : null
                    };
                })
                .OrderBy(plan =>
                    plan.NormalizedExternalName)
                .ToList();

        result.AliasesMatchedToPlayersCount =
            aliasPlans.Count(plan =>
                plan.PlayerId != null);

        result.AliasesWithoutPlayerCount =
            aliasPlans.Count(plan =>
                plan.PlayerId == null);

        //
        // Vérifier si la migration historique est appliquée.
        //

        result.HistoricalSchemaExists =
            await TableExistsAsync(
                "HistoricalPlayerAliases",
                cancellationToken)
            &&
            await TableExistsAsync(
                "HistoricalPerformances",
                cancellationToken)
            &&
            await TableExistsAsync(
                "HistoricalPerformanceSources",
                cancellationToken);

        HashSet<string> existingAliasNames =
            new(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> existingExternalKeys =
            new(
                StringComparer.OrdinalIgnoreCase);

        if (result.HistoricalSchemaExists)
        {
            List<string> aliasNames =
                await _db.HistoricalPlayerAliases
                    .AsNoTracking()
                    .Where(alias =>
                        alias.SourceSystem ==
                        "YahooGroups")
                    .Select(alias =>
                        alias.NormalizedExternalName)
                    .ToListAsync(cancellationToken);

            existingAliasNames =
                aliasNames.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

            List<string> externalKeys =
                await _db.HistoricalPerformances
                    .AsNoTracking()
                    .Select(performance =>
                        performance.ExternalKey)
                    .ToListAsync(cancellationToken);

            existingExternalKeys =
                externalKeys.ToHashSet(
                    StringComparer.OrdinalIgnoreCase);
        }

        result.AliasesAlreadyExistingCount =
            aliasPlans.Count(plan =>
                existingAliasNames.Contains(
                    plan.NormalizedExternalName));

        result.AliasesToCreateCount =
            aliasPlans.Count -
            result.AliasesAlreadyExistingCount;

        //
        // Performances existantes ou à créer.
        //

        List<RecordPlan> recordPlans =
            validRecords
                .Select(record =>
                    new RecordPlan
                    {
                        Record = record,

                        ExternalKey =
                            BuildExternalKey(record)
                    })
                .ToList();

        result.PerformancesAlreadyExistingCount =
            recordPlans.Count(plan =>
                existingExternalKeys.Contains(
                    plan.ExternalKey));

        List<RecordPlan> recordsToCreate =
            recordPlans
                .Where(plan =>
                    !existingExternalKeys.Contains(
                        plan.ExternalKey))
                .ToList();

        result.PerformancesToCreateCount =
            recordsToCreate.Count;

        result.SourcesToCreateCount =
            recordsToCreate.Sum(plan =>
                plan.Record.Sources
                    .Select(source =>
                        source.MessageId)
                    .Distinct()
                    .Count());

        return result;
    }

    private async Task<bool> TableExistsAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection =
            _db.Database.GetDbConnection();

        bool shouldClose =
            connection.State !=
            ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(
                cancellationToken);
        }

        try
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

            var parameter =
                command.CreateParameter();

            parameter.ParameterName =
                "$tableName";

            parameter.Value =
                tableName;

            command.Parameters.Add(
                parameter);

            object? scalar =
                await command.ExecuteScalarAsync(
                    cancellationToken);

            return Convert.ToInt64(scalar) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<List<YahooHistoricalRecord>>
        ReadRecordsAsync(
            string jsonFilePath,
            CancellationToken cancellationToken)
    {
        await using FileStream input =
            File.OpenRead(jsonFilePath);

        List<YahooHistoricalRecord>? records =
            await JsonSerializer.DeserializeAsync<
                List<YahooHistoricalRecord>>(
                    input,
                    JsonOptions,
                    cancellationToken);

        return records ?? [];
    }

    private static bool IsValidRecord(
        YahooHistoricalRecord record)
    {
        if (string.IsNullOrWhiteSpace(
                record.PlayerName))
        {
            return false;
        }

        if (!CourseNamesByCode.ContainsKey(
                record.TrackCode))
        {
            return false;
        }

        if (!string.Equals(
                record.TimeType,
                "ThreeLap",
                StringComparison.OrdinalIgnoreCase)
            &&
            !string.Equals(
                record.TimeType,
                "Flap",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (record.TimeMilliseconds <= 0)
            return false;

        if (record.Frequency is not null
            and not 50
            and not 60)
        {
            return false;
        }

        return true;
    }

    private static string GetCourseName(
        string trackCode)
    {
        if (!CourseNamesByCode.TryGetValue(
                trackCode,
                out string? courseName))
        {
            throw new InvalidOperationException(
                $"Code circuit inconnu : {trackCode}");
        }

        return courseName;
    }

    private static string BuildExternalKey(
        YahooHistoricalRecord record)
    {
        string rawKey =
            string.Join(
                "|",
                "YahooGroups",

                NormalizePlayerName(
                    record.PlayerName),

                record.TrackCode
                    .Trim()
                    .ToUpperInvariant(),

                record.TimeType
                    .Trim()
                    .ToUpperInvariant(),

                record.Frequency?.ToString()
                    ?? "<NO_FREQUENCY>",

                record.TimeMilliseconds,

                record.FirstSeenAt
                    .ToUniversalTime()
                    .ToUnixTimeMilliseconds());

        byte[] hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    rawKey));

        return Convert.ToHexString(hash);
    }

    private static string NormalizePlayerName(
        string? value)
    {
        return NormalizeText(value)
            .Replace("’", "'")
            .Replace("‘", "'");
    }

    private static string NormalizeText(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized =
            value
                .Trim()
                .Normalize(
                    NormalizationForm.FormKC);

        normalized = Regex.Replace(
            normalized,
            @"\s+",
            " ");

        return normalized.ToLowerInvariant();
    }

    private sealed class PlayerAliasPlan
    {
        public string ExternalName { get; set; } =
            string.Empty;

        public string NormalizedExternalName { get; set; } =
            string.Empty;

        public int? PlayerId { get; set; }
    }

    private sealed class RecordPlan
    {
        public required YahooHistoricalRecord Record { get; set; }

        public required string ExternalKey { get; set; }
    }
}