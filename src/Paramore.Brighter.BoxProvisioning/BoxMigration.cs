using System.Collections.Generic;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// A concrete migration step with a version, description, and up script.
/// </summary>
/// <param name="Version">Monotonically increasing version number.</param>
/// <param name="Description">Human-readable description of what this migration does.</param>
/// <param name="UpScript">Script to apply the migration.</param>
/// <param name="LogicalColumns">
/// The full set of logical (cross-backend) column names present on the table after this
/// migration has been applied. Excludes backend-specific housekeeping columns. Consumed by
/// <c>DetectCurrentVersionAsync</c> for column-name-superset detection on existing tables.
/// </param>
/// <param name="SourceReference">
/// Optional commit/PR reference (e.g. <c>"&lt;short-sha&gt; / #&lt;pr-number&gt;"</c>) identifying
/// where this schema change originally landed upstream. Diagnostic only — not used at runtime.
/// </param>
/// <param name="IdempotencyCheckSql">
/// Optional SQL fragment used as an idempotency guard before executing <see cref="UpScript"/>.
/// Non-null on backends (notably SQLite V2+) whose DDL grammar cannot embed inline existence
/// checks; <c>null</c> where the guard lives inside <see cref="UpScript"/> directly.
/// </param>
public record BoxMigration(
    int Version,
    string Description,
    string UpScript,
    ISet<string> LogicalColumns,
    string? SourceReference = null,
    string? IdempotencyCheckSql = null) : IAmABoxMigration;
