using System.Collections.Generic;

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

    /// <summary>
    /// The full set of logical (cross-backend) column names that exist on the table after this
    /// migration has been applied. Used by <c>DetectCurrentVersionAsync</c> to walk
    /// <c>V_latest..V1</c> and return the first version whose <see cref="LogicalColumns"/> is a
    /// subset of the columns observed on an existing table (column-name-superset match).
    /// <para>
    /// Implementations should populate this set once at construction and never mutate it.
    /// <see cref="ISet{T}"/> is used (rather than <c>IReadOnlySet{T}</c>) because
    /// <c>IReadOnlySet{T}</c> is unavailable on <c>netstandard2.0</c>; the read-only-by-convention
    /// invariant is enforced at the implementation level.
    /// </para>
    /// <para>
    /// Excludes backend-specific housekeeping columns (e.g. MSSQL <c>Id</c> identity PK,
    /// MySQL <c>Created</c>/<c>CreatedID</c>, Postgres <c>Id</c> BIGSERIAL) — those live inside
    /// each backend's V1 DDL and do not participate in logical version numbering.
    /// </para>
    /// </summary>
    ISet<string> LogicalColumns { get; }

    /// <summary>
    /// Optional reference identifying the commit and/or PR in which the schema change documented
    /// by this migration originally landed in the upstream Brighter codebase. Format is free-form
    /// (typically <c>"&lt;short-sha&gt; / #&lt;pr-number&gt;"</c>) and intended for diagnostics
    /// and archaeology, not for runtime behaviour. <c>null</c> for V1 (no single source commit).
    /// </summary>
    string? SourceReference { get; }

    /// <summary>
    /// Optional SQL fragment used as an idempotency guard before executing
    /// <see cref="UpScript"/>. Required on backends whose DDL grammar cannot embed
    /// <c>IF NOT EXISTS</c> column-existence checks inline (notably SQLite for V2+); <c>null</c>
    /// where the guard is folded into <see cref="UpScript"/> directly (MSSQL/Postgres/MySQL).
    /// When non-null, the runner evaluates this query and skips <see cref="UpScript"/> if the
    /// result indicates the migration has already been applied.
    /// </summary>
    string? IdempotencyCheckSql { get; }
}
