namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class HistoricalImportAnalysisResult
{
    public int InputRecordsCount { get; set; }

    public int MappableRecordsCount { get; set; }

    public int BlockedRecordsCount { get; set; }

    public int UniqueExternalPlayersCount { get; set; }

    public int MatchedPlayersCount { get; set; }

    public int UnmatchedPlayersCount { get; set; }

    public int AmbiguousPlayersCount { get; set; }

    public int MatchedCoursesCount { get; set; }

    public int MissingCoursesCount { get; set; }

    public int CreatableCoursesCount { get; set; }

    public int UnsupportedCoursesCount { get; set; }

    public int RecordsUsingCreatableCoursesCount { get; set; }

    public List<HistoricalPlayerMatch> Players { get; set; } = [];

    public List<HistoricalCourseMatch> Courses { get; set; } = [];

    public List<HistoricalMappedRecord> MappableRecords { get; set; } = [];

    public List<HistoricalBlockedRecord> BlockedRecords { get; set; } = [];
}

public sealed class HistoricalPlayerMatch
{
    public string ExternalName { get; set; } =
        string.Empty;

    public string NormalizedExternalName { get; set; } =
        string.Empty;

    public string Status { get; set; } =
        "Unmatched";

    public int? PlayerId { get; set; }

    public int? SitePlayerId { get; set; }

    public string? MatchedPlayerName { get; set; }

    public List<HistoricalPlayerCandidate> Candidates { get; set; } = [];

    public int RecordsCount { get; set; }
}

public sealed class HistoricalPlayerCandidate
{
    public int PlayerId { get; set; }

    public int SitePlayerId { get; set; }

    public string Name { get; set; } =
        string.Empty;

    public string? Country { get; set; }
}

public sealed class HistoricalCourseMatch
{
    public string TrackCode { get; set; } =
        string.Empty;

    public string ExpectedCourseName { get; set; } =
        string.Empty;

    public string Status { get; set; } =
        "Unsupported";

    public int? CourseId { get; set; }

    public string? MatchedCourseName { get; set; }

    public int RecordsCount { get; set; }
}

public sealed class HistoricalMappedRecord
{
    public required YahooHistoricalRecord SourceRecord { get; set; }

    public int HistoricalPlayerMatchIndex { get; set; }

    public int? PlayerId { get; set; }

    public int? CourseId { get; set; }

    public string CourseName { get; set; } =
        string.Empty;

    public string CourseMappingStatus { get; set; } =
        string.Empty;
}

public sealed class HistoricalBlockedRecord
{
    public required YahooHistoricalRecord SourceRecord { get; set; }

    public List<string> Reasons { get; set; } = [];
}