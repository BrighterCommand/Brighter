namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Describes the current state of a box table in the database.
/// </summary>
/// <param name="TableExists">Whether the box table exists in the database.</param>
/// <param name="HistoryExists">Whether the migration history table exists and has entries for this box.</param>
/// <param name="CurrentVersion">The current schema version of the box table.</param>
public record BoxTableState(bool TableExists, bool HistoryExists, int CurrentVersion);
