namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Describes a single schema migration step.
/// </summary>
public interface IAmABoxMigration
{
    /// <summary>Monotonically increasing version number.</summary>
    int Version { get; }

    /// <summary>Human-readable description of what this migration does.</summary>
    string Description { get; }

    /// <summary>Script to apply the migration (SQL for relational backends).</summary>
    string UpScript { get; }
}
