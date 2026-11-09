namespace MKDDTracker.Scraper.Models.Evolution;

/// <summary>
/// Identifies the time change or roster event that caused a direct movement.
/// The target is the player whose evolution is being analysed.
/// </summary>
public enum MkddMovementCause
{
    TargetDriven,
    OpponentDriven,
    BothImproved,
    TargetRegressed,
    OpponentRegressed,
    BothRegressed,
    NewEntry,
    PlayerRemoved,
    NoTimeChange
}
