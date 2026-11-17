namespace MKDDTracker.Scraper.Models.Evolution;

/// <summary>
/// Describes how another player changed position relative to the target player
/// between two snapshots.
/// </summary>
public enum MkddMovementRelation
{
    NoDirectCrossing,
    TargetPassedPlayer,
    PlayerPassedTarget,
    TieCreated,
    TieBroken,
    NewEntry,
    PlayerRemoved
}
