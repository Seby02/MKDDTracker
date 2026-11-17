namespace MKDDTracker.Scraper.Models.Dashboard;

public sealed record MkddHistoricalDashboard(
    int TotalPerformanceCount,
    int HistoricalIdentityCount,
    int ModernLinkedPlayerCount,
    int CourseCount,
    DateTimeOffset? FirstSeenAt,
    DateTimeOffset? LastSeenAt,
    int YahooGroupsCount,
    int MkddTrackerCount,
    int ThreeLapCount,
    int FlapCount,
    int Hz50Count,
    int Hz60Count,
    int UnknownFrequencyCount,
    IReadOnlyList<MkddHistoricalDashboardEvent> RecentEvents);

public sealed record MkddHistoricalDashboardEvent(
    DateTimeOffset FirstSeenAt,
    string PlayerName,
    string PlayerKey,
    int? PlayerId,
    int HistoricalPlayerAliasId,
    string CourseName,
    string TimeType,
    int? Frequency,
    TimeSpan Time,
    IReadOnlyList<string> Sources);
