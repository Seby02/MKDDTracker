using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;

namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class MkddHistoricalImportService
{
    private const int BatchSize = 1_000;

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

    public MkddHistoricalImportService(
        MkddDbContext db)
    {
        _db = db;
    }

    public async Task<HistoricalImportExecutionResult> ImportAsync(
        string jsonFilePath,
        CancellationToken cancellationToken = default)
    {
        var result =
            new HistoricalImportExecutionResult();

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

        if (result.InvalidRecordsCount > 0)
        {
            result.Errors.Add(
                $"{result.InvalidRecordsCount:N0} " +
                "enregistrement(s) invalide(s). Import annulé.");

            return result;
        }

        //
        // Préparer les correspondances avant la transaction.
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
                            NormalizePlayerName(player.Name),
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToList(),
                        StringComparer.OrdinalIgnoreCase);

        List<PlayerAliasPlan> aliasPlans =
            BuildAliasPlans(
                validRecords,
                playersByNormalizedName);

        result.AliasesMatchedToPlayersCount =
            aliasPlans.Count(plan =>
                plan.PlayerId != null);

        result.AliasesWithoutPlayerCount =
            aliasPlans.Count(plan =>
                plan.PlayerId == null);

        List<string> existingExternalKeyList =
            await _db.HistoricalPerformances
                .AsNoTracking()
                .Select(performance =>
                    performance.ExternalKey)
                .ToListAsync(cancellationToken);

        HashSet<string> existingExternalKeys =
            existingExternalKeyList.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        List<PerformancePlan> performancePlans =
            validRecords
                .Select(record =>
                    new PerformancePlan
                    {
                        Record = record,
                        ExternalKey =
                            BuildExternalKey(record)
                    })
                .ToList();

        result.PerformancesExistingCount =
            performancePlans.Count(plan =>
                existingExternalKeys.Contains(
                    plan.ExternalKey));

        List<PerformancePlan> plansToInsert =
            performancePlans
                .Where(plan =>
                    !existingExternalKeys.Contains(
                        plan.ExternalKey))
                .ToList();

        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            //
            // 1. Créer les circuits absents.
            //

            List<MkddCourse> existingCourses =
                await _db.Courses
                    .ToListAsync(cancellationToken);

            Dictionary<string, MkddCourse> coursesByName =
                existingCourses
                    .GroupBy(
                        course =>
                            NormalizeText(course.Name),
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Single(),
                        StringComparer.OrdinalIgnoreCase);

            List<string> requiredCourseNames =
                validRecords
                    .Select(record =>
                        GetCourseName(record.TrackCode))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();

            foreach (string courseName
                     in requiredCourseNames)
            {
                string normalizedName =
                    NormalizeText(courseName);

                if (coursesByName.ContainsKey(
                        normalizedName))
                {
                    continue;
                }

                var course =
                    new MkddCourse
                    {
                        Name = courseName
                    };

                _db.Courses.Add(course);

                coursesByName.Add(
                    normalizedName,
                    course);

                result.CoursesCreatedCount++;
            }

            await _db.SaveChangesAsync(
                cancellationToken);

            //
            // 2. Créer ou réutiliser les alias historiques.
            //

            List<MkddHistoricalPlayerAlias> existingAliasList =
                await _db.HistoricalPlayerAliases
                    .Where(alias =>
                        alias.SourceSystem == "YahooGroups")
                    .ToListAsync(cancellationToken);

            Dictionary<string, MkddHistoricalPlayerAlias>
                aliasesByNormalizedName =
                    existingAliasList.ToDictionary(
                        alias =>
                            alias.NormalizedExternalName,
                        StringComparer.OrdinalIgnoreCase);

            foreach (PlayerAliasPlan plan
                     in aliasPlans)
            {
                if (aliasesByNormalizedName.ContainsKey(
                        plan.NormalizedExternalName))
                {
                    continue;
                }

                var alias =
                    new MkddHistoricalPlayerAlias
                    {
                        SourceSystem =
                            "YahooGroups",

                        ExternalName =
                            plan.ExternalName,

                        NormalizedExternalName =
                            plan.NormalizedExternalName,

                        PlayerId =
                            plan.PlayerId,

                        Confidence =
                            plan.PlayerId != null
                                ? 100
                                : 0,

                        ResolutionStatus =
                            plan.PlayerId != null
                                ? "Matched"
                                : "Unresolved"
                    };

                _db.HistoricalPlayerAliases.Add(alias);

                aliasesByNormalizedName.Add(
                    plan.NormalizedExternalName,
                    alias);

                result.AliasesCreatedCount++;
            }

            await _db.SaveChangesAsync(
                cancellationToken);

            //
            // Les dictionnaires contiennent maintenant
            // les identifiants SQLite générés.
            //

            Dictionary<string, int> courseIdsByName =
                coursesByName.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.Id,
                    StringComparer.OrdinalIgnoreCase);

            Dictionary<string, int> aliasIdsByName =
                aliasesByNormalizedName.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value.Id,
                    StringComparer.OrdinalIgnoreCase);

            //
            // 3. Insérer les performances par lots.
            //

            int processed = 0;

            foreach (PerformancePlan plan
                     in plansToInsert)
            {
                YahooHistoricalRecord record =
                    plan.Record;

                string normalizedPlayerName =
                    NormalizePlayerName(
                        record.PlayerName);

                string courseName =
                    GetCourseName(
                        record.TrackCode);

                int aliasId =
                    aliasIdsByName[
                        normalizedPlayerName];

                int courseId =
                    courseIdsByName[
                        NormalizeText(courseName)];

                var performance =
                    new MkddHistoricalPerformance
                    {
                        ExternalKey =
                            plan.ExternalKey,

                        HistoricalPlayerAliasId =
                            aliasId,

                        CourseId =
                            courseId,

                        TimeType =
                            record.TimeType,

                        Frequency =
                            record.Frequency,

                        Time =
                            TimeSpan.FromMilliseconds(
                                record.TimeMilliseconds),

                        FirstSeenAt =
                            record.FirstSeenAt,

                        DateSource =
                            string.IsNullOrWhiteSpace(
                                record.DateSource)
                                ? "YahooFirstSeenDate"
                                : record.DateSource,

                        SequenceNumber =
                            record.SequenceNumber,

                        PreviousTime =
                            record.PreviousTimeMilliseconds
                                is long previous
                                    ? TimeSpan.FromMilliseconds(
                                        previous)
                                    : null,

                        Improvement =
                            record.ImprovementMilliseconds
                                is long improvement
                                    ? TimeSpan.FromMilliseconds(
                                        improvement)
                                    : null,

                        OccurrenceCount =
                            record.OccurrenceCount,

                        SourceSystem =
                            string.IsNullOrWhiteSpace(
                                record.SourceSystem)
                                ? "YahooGroups"
                                : record.SourceSystem,

                        ImportStatus =
                            record.ImportStatus,

                        ImportReasonsJson =
                            SerializeStringList(
                                record.ImportReasons),

                        ValidationWarningsJson =
                            SerializeStringList(
                                record.ValidationWarnings)
                    };

                foreach (YahooHistoricalRecordSource source
                         in record.Sources
                             .GroupBy(item =>
                                 item.MessageId)
                             .Select(group =>
                                 group.First()))
                {
                    performance.Sources.Add(
                        new MkddHistoricalPerformanceSource
                        {
                            MessageId =
                                source.MessageId,

                            PublishedAt =
                                source.PublishedAt,

                            SourceFile =
                                source.SourceFile,

                            OriginalTime =
                                source.OriginalTime,

                            SourceLine =
                                source.SourceLine,

                            PlayerConfidence =
                                source.PlayerConfidence,

                            TimeTypeConfidence =
                                source.TimeTypeConfidence,

                            ImportStatus =
                                source.ImportStatus
                        });

                    result.SourcesCreatedCount++;
                }

                _db.HistoricalPerformances.Add(
                    performance);

                result.PerformancesCreatedCount++;
                processed++;

                if (processed % BatchSize == 0)
                {
                    await _db.SaveChangesAsync(
                        cancellationToken);

                    //
                    // Les objets des lots précédents n'ont plus
                    // besoin de rester suivis par EF.
                    //

                    _db.ChangeTracker.Clear();

                    Console.WriteLine(
                        $"Import historique : " +
                        $"{processed:N0} / " +
                        $"{plansToInsert.Count:N0}");
                }
            }

            await _db.SaveChangesAsync(
                cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);

            result.TransactionCommitted = true;

            return result;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(
                CancellationToken.None);

            result.Errors.Add(
                $"{exception.GetType().Name}: " +
                $"{exception.Message}");

            result.TransactionCommitted = false;

            return result;
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

    private static List<PlayerAliasPlan> BuildAliasPlans(
        IEnumerable<YahooHistoricalRecord> records,
        IReadOnlyDictionary<
            string,
            List<MkddPlayer>>
            playersByNormalizedName)
    {
        return records
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

                MkddPlayer? matchedPlayer =
                    candidates.Count == 1
                        ? candidates[0]
                        : null;

                return new PlayerAliasPlan
                {
                    ExternalName =
                        group.First().PlayerName,

                    NormalizedExternalName =
                        group.Key,

                    PlayerId =
                        matchedPlayer?.Id
                };
            })
            .OrderBy(plan =>
                plan.NormalizedExternalName)
            .ToList();
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

        return record.Frequency is null or 50 or 60;
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
        return HistoricalPlayerNameNormalizer.NormalizePlayerName(value);
    }

    private static string NormalizeText(
        string? value)
    {
        return HistoricalPlayerNameNormalizer.NormalizeText(value);
    }

    private static string? SerializeStringList(
        IEnumerable<string> values)
    {
        List<string> normalized =
            values
                .Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList();

        return normalized.Count == 0
            ? null
            : JsonSerializer.Serialize(
                normalized);
    }

    private sealed class PlayerAliasPlan
    {
        public string ExternalName { get; set; } =
            string.Empty;

        public string NormalizedExternalName { get; set; } =
            string.Empty;

        public int? PlayerId { get; set; }
    }

    private sealed class PerformancePlan
    {
        public required YahooHistoricalRecord Record { get; set; }

        public required string ExternalKey { get; set; }
    }
}