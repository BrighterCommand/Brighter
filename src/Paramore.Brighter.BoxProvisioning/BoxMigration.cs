namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// A concrete migration step with a version, description, and up script.
/// </summary>
/// <param name="Version">Monotonically increasing version number.</param>
/// <param name="Description">Human-readable description of what this migration does.</param>
/// <param name="UpScript">Script to apply the migration.</param>
public record BoxMigration(int Version, string Description, string UpScript) : IAmABoxMigration;
