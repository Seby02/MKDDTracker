using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;

namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class MkddHistoricalImportAnalyzer
{
    private readonly MkddDbContext _db;

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

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public MkddHistoricalImportAnalyzer(
        MkddDbContext db)
    {
        _db = db;
    }

    public async Task<HistoricalImportAnalysisResult> AnalyzeAsync(
        string jsonFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException(
                "Le fichier d'import historique est introuvable.",
                jsonFilePath);
        }

        List<YahooHistoricalRecord> records =
            await ReadRecordsAsync(
                jsonFilePath,
                cancellationToken);

        List<MkddPlayer> databasePlayers =
            await _db.Players
                .AsNoTracking()
                .OrderBy(player => player.Id)
                .ToListAsync(cancellationToken);

        List<MkddCourse> databaseCourses =
            await _db.Courses
                .AsNoTracking()
                .OrderBy(course => course.Id)
                .ToListAsync(cancellationToken);

        List<HistoricalPlayerMatch> playerMatches =
            BuildPlayerMatches(
                records,
                databasePlayers);

        List<HistoricalCourseMatch> courseMatches =
            BuildCourseMatches(
                records,
                databaseCourses);

        Dictionary<string, HistoricalPlayerMatch> playerMatchesByName =
            playerMatches.ToDictionary(
                match => match.NormalizedExternalName,
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, HistoricalCourseMatch> courseMatchesByCode =
            courseMatches.ToDictionary(
                match => match.TrackCode,
                StringComparer.OrdinalIgnoreCase);

        var mappableRecords =
            new List<HistoricalMappedRecord>();

        var blockedRecords =
            new List<HistoricalBlockedRecord>();

        for (int index = 0;
             index < records.Count;
             index++)
        {
            YahooHistoricalRecord record =
                records[index];

            var reasons =
                new List<string>();

            string normalizedPlayerName =
                NormalizePlayerName(
                    record.PlayerName);

            if (!playerMatchesByName.TryGetValue(
                    normalizedPlayerName,
                    out HistoricalPlayerMatch? playerMatch))
            {
                reasons.Add(
                    "PlayerMappingMissing");
            }
            else if (playerMatch.Status == "Ambiguous")
            {
                reasons.Add(
                    "PlayerMappingAmbiguous");
            }

            if (!courseMatchesByCode.TryGetValue(
        record.TrackCode,
        out HistoricalCourseMatch? courseMatch))
            {
                reasons.Add(
                    "CourseMappingMissing");
            }
            else if (courseMatch.Status == "Unsupported")
            {
                reasons.Add(
                    "UnsupportedTrackCode");
            }
            else if (courseMatch.Status == "Ambiguous")
            {
                reasons.Add(
                    "CourseMappingAmbiguous");
            }

            if (!IsSupportedTimeType(record.TimeType))
            {
                reasons.Add(
                    "UnsupportedTimeType");
            }

            if (record.TimeMilliseconds <= 0)
            {
                reasons.Add(
                    "InvalidTime");
            }

            if (record.Frequency is not null and not 50 and not 60)
            {
                reasons.Add(
                    "InvalidFrequency");
            }

            if (string.IsNullOrWhiteSpace(
                    record.SourceSystem))
            {
                reasons.Add(
                    "SourceSystemMissing");
            }

            if (reasons.Count > 0)
            {
                blockedRecords.Add(
                    new HistoricalBlockedRecord
                    {
                        SourceRecord = record,
                        Reasons = reasons
                            .Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .OrderBy(reason => reason)
                            .ToList()
                    });

                continue;
            }

            int playerMatchIndex =
                playerMatches.IndexOf(
                    playerMatch!);

            mappableRecords.Add(
                new HistoricalMappedRecord
                {
                    SourceRecord = record,

                    HistoricalPlayerMatchIndex =
                        playerMatchIndex,

                    //
                    // Nullable si le joueur Yahoo n'a pas
                    // encore de joueur moderne correspondant.
                    //

                    PlayerId =
                        playerMatch!.Status == "Matched"
                            ? playerMatch.PlayerId
                            : null,

                    CourseId =
    courseMatch!.CourseId,

                    CourseName =
    courseMatch.ExpectedCourseName,

                    CourseMappingStatus =
    courseMatch.Status
                });
        }

        return new HistoricalImportAnalysisResult
        {
            InputRecordsCount =
        records.Count,

            MappableRecordsCount =
        mappableRecords.Count,

            BlockedRecordsCount =
        blockedRecords.Count,

            UniqueExternalPlayersCount =
        playerMatches.Count,

            MatchedPlayersCount =
        playerMatches.Count(match =>
            match.Status == "Matched"),

            UnmatchedPlayersCount =
        playerMatches.Count(match =>
            match.Status == "Unmatched"),

            AmbiguousPlayersCount =
        playerMatches.Count(match =>
            match.Status == "Ambiguous"),

            MatchedCoursesCount =
        courseMatches.Count(match =>
            match.Status == "Matched"),

            CreatableCoursesCount =
        courseMatches.Count(match =>
            match.Status == "Creatable"),

            UnsupportedCoursesCount =
        courseMatches.Count(match =>
            match.Status == "Unsupported"),

            MissingCoursesCount =
        courseMatches.Count(match =>
            match.Status != "Matched"),

            RecordsUsingCreatableCoursesCount =
        mappableRecords.Count(record =>
            record.CourseMappingStatus == "Creatable"),

            Players =
        playerMatches,

            Courses =
        courseMatches,

            MappableRecords =
        mappableRecords,

            BlockedRecords =
        blockedRecords
        };
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

    private static List<HistoricalPlayerMatch>
        BuildPlayerMatches(
            IEnumerable<YahooHistoricalRecord> records,
            IReadOnlyCollection<MkddPlayer> databasePlayers)
    {
        Dictionary<string, List<MkddPlayer>> playersByNormalizedName =
            databasePlayers
                .GroupBy(
                    player => NormalizePlayerName(
                        player.Name),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList(),
                    StringComparer.OrdinalIgnoreCase);

        return records
            .Where(record =>
                !string.IsNullOrWhiteSpace(
                    record.PlayerName))
            .GroupBy(
                record => NormalizePlayerName(
                    record.PlayerName),
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                YahooHistoricalRecord representative =
                    group.First();

                playersByNormalizedName.TryGetValue(
                    group.Key,
                    out List<MkddPlayer>? candidates);

                candidates ??= [];

                string status =
                    candidates.Count switch
                    {
                        0 => "Unmatched",
                        1 => "Matched",
                        _ => "Ambiguous"
                    };

                MkddPlayer? matchedPlayer =
                    candidates.Count == 1
                        ? candidates[0]
                        : null;

                return new HistoricalPlayerMatch
                {
                    ExternalName =
                        representative.PlayerName,

                    NormalizedExternalName =
                        group.Key,

                    Status =
                        status,

                    PlayerId =
                        matchedPlayer?.Id,

                    SitePlayerId =
                        matchedPlayer?.SitePlayerId,

                    MatchedPlayerName =
                        matchedPlayer?.Name,

                    Candidates =
                        candidates
                            .Select(player =>
                                new HistoricalPlayerCandidate
                                {
                                    PlayerId =
                                        player.Id,

                                    SitePlayerId =
                                        player.SitePlayerId,

                                    Name =
                                        player.Name,

                                    Country =
                                        player.Country
                                })
                            .OrderBy(candidate =>
                                candidate.PlayerId)
                            .ToList(),

                    RecordsCount =
                        group.Count()
                };
            })
            .OrderByDescending(match =>
                match.RecordsCount)
            .ThenBy(match =>
                match.ExternalName)
            .ToList();
    }

    private static List<HistoricalCourseMatch>BuildCourseMatches(
        IEnumerable<YahooHistoricalRecord> records,
        IReadOnlyCollection<MkddCourse> databaseCourses)
    {
        Dictionary<string, List<MkddCourse>> coursesByNormalizedName =
            databaseCourses
                .GroupBy(
                    course => NormalizeText(course.Name),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList(),
                    StringComparer.OrdinalIgnoreCase);

        return records
            .GroupBy(
                record => record.TrackCode,
                StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                bool codeIsSupported =
                    CourseNamesByCode.TryGetValue(
                        group.Key,
                        out string? expectedCourseName);

                if (!codeIsSupported ||
                    string.IsNullOrWhiteSpace(expectedCourseName))
                {
                    return new HistoricalCourseMatch
                    {
                        TrackCode = group.Key,
                        ExpectedCourseName = string.Empty,
                        Status = "Unsupported",
                        RecordsCount = group.Count()
                    };
                }

                coursesByNormalizedName.TryGetValue(
                    NormalizeText(expectedCourseName),
                    out List<MkddCourse>? candidates);

                candidates ??= [];

                string status = candidates.Count switch
                {
                    0 => "Creatable",
                    1 => "Matched",
                    _ => "Ambiguous"
                };

                MkddCourse? matchedCourse =
                    candidates.Count == 1
                        ? candidates[0]
                        : null;

                return new HistoricalCourseMatch
                {
                    TrackCode = group.Key,
                    ExpectedCourseName = expectedCourseName,
                    Status = status,
                    CourseId = matchedCourse?.Id,
                    MatchedCourseName = matchedCourse?.Name,
                    RecordsCount = group.Count()
                };
            })
            .OrderBy(match => match.TrackCode)
            .ToList();
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

    private static bool IsSupportedTimeType(
        string? timeType)
    {
        return string.Equals(
                   timeType,
                   "ThreeLap",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   timeType,
                   "Flap",
                   StringComparison.OrdinalIgnoreCase);
    }
}