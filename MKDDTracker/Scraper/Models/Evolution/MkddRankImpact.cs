namespace MKDDTracker.Scraper.Models.Evolution;

public sealed record MkddRankImpact(
    int PlayerId,
    string PlayerName,
    int PreviousRank,
    int CurrentRank,
    TimeSpan PreviousTime,
    TimeSpan CurrentTime,
    int RankImpact);