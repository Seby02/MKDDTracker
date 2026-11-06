namespace MKDDTracker.Scraper.Models;

public sealed record MkddPlayerSnapshotComparison(
    DateOnly PreviousDate,
    DateOnly CurrentDate,
    int PreviousRank,
    int CurrentRank,
    TimeSpan PreviousTime,
    TimeSpan CurrentTime,
    int RankChange,
    TimeSpan TimeChange,
    int PlacesGainedFromOwnImprovement,
    int PlacesGainedFromOthers);