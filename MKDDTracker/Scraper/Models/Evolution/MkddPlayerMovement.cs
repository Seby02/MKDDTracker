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
    MkddMovementRelation Relation,
    MkddMovementCause Cause)
{
    /// <summary>
    /// Stable identifier of the target player involved in this movement.
    /// </summary>
    public int RelatedPlayerId { get; init; }

    /// <summary>
    /// False for a player who entered the ranking in the current snapshot.
    /// </summary>
    public bool WasPresentBefore { get; init; } = true;

    /// <summary>
    /// False for a player who disappeared from the current snapshot.
    /// </summary>
    public bool IsPresentNow { get; init; } = true;

    public int? OldRank => WasPresentBefore ? PreviousRank : null;

    public int? NewRank => IsPresentNow ? CurrentRank : null;

    public TimeSpan? OldTime => WasPresentBefore ? PreviousTime : null;

    public TimeSpan? NewTime => IsPresentNow ? CurrentTime : null;

    /// <summary>
    /// Positive means the player gained places; negative means they lost places.
    /// </summary>
    public int? RankDelta =>
        WasPresentBefore && IsPresentNow
            ? PreviousRank - CurrentRank
            : null;

    /// <summary>
    /// Negative means the player improved their time.
    /// </summary>
    public TimeSpan? TimeDelta =>
        WasPresentBefore && IsPresentNow
            ? CurrentTime - PreviousTime
            : null;

    // Kept for compatibility with the existing renderer and report model.
    public TimeSpan TimeChange =>
        TimeDelta ?? TimeSpan.Zero;
}
