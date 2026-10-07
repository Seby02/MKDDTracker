namespace MKDDTracker.Scraper.Models.Evolution;

public sealed record MkddPlayerMovement(
    int PlayerId,
    string PlayerName,
    DateOnly PreviousDate,
    DateOnly CurrentDate,
    int PreviousRank,
    int CurrentRank,
    TimeSpan PreviousTime,
    TimeSpan CurrentTime,
    int RankChange,
    MkddMovementRelation Relation)
{
    public TimeSpan TimeChange =>
        CurrentTime - PreviousTime;
}