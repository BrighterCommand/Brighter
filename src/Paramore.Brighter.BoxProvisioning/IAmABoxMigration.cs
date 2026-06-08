#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Collections.Generic;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Describes a single schema migration step.
/// </summary>
public interface IAmABoxMigration
{
    /// <summary>Monotonically increasing version number.</summary>
    MigrationVersion Version { get; }

    /// <summary>Human-readable description of what this migration does.</summary>
    MigrationDescription Description { get; }

    /// <summary>Script to apply the migration (SQL for relational backends).</summary>
    SqlScript UpScript { get; }

    /// <summary>
    /// The full set of logical (cross-backend) column names that exist on the table after this
    /// migration has been applied. Used by <c>DetectCurrentVersionAsync</c> to walk
    /// <c>V_latest..V1</c> and return the first version whose <see cref="LogicalColumns"/> is a
    /// subset of the columns observed on an existing table (column-name-superset match).
    /// <para>
    /// Exposed as <see cref="IReadOnlyCollection{T}"/> so the public surface is immutable;
    /// implementations typically back this with a <see cref="HashSet{T}"/> using the
    /// backend-appropriate <see cref="StringComparer"/> (Ordinal vs OrdinalIgnoreCase per
    /// ADR 0057 §1) populated once at construction. <c>IReadOnlySet{T}</c> would be a tighter
    /// fit but is unavailable on <c>netstandard2.0</c>.
    /// </para>
    /// <para>
    /// Excludes backend-specific housekeeping columns (e.g. MSSQL <c>Id</c> identity PK,
    /// MySQL <c>Created</c>/<c>CreatedID</c>, Postgres <c>Id</c> BIGSERIAL) — those live inside
    /// each backend's V1 DDL and do not participate in logical version numbering.
    /// </para>
    /// </summary>
    IReadOnlyCollection<string> LogicalColumns { get; }

    /// <summary>
    /// Optional reference identifying the commit and/or PR in which the schema change documented
    /// by this migration originally landed in the upstream Brighter codebase. Format is free-form
    /// (typically <c>"&lt;short-sha&gt; / #&lt;pr-number&gt;"</c>) and intended for diagnostics
    /// and archaeology, not for runtime behaviour. <c>null</c> for V1 (no single source commit).
    /// </summary>
    SourceReference? SourceReference { get; }

    /// <summary>
    /// Optional SQL fragment used as an idempotency guard before executing
    /// <see cref="UpScript"/>. Required on backends whose DDL grammar cannot embed
    /// <c>IF NOT EXISTS</c> column-existence checks inline (notably SQLite for V2+); <c>null</c>
    /// where the guard is folded into <see cref="UpScript"/> directly (MSSQL/Postgres/MySQL).
    /// When non-null, the runner evaluates this query and skips <see cref="UpScript"/> if the
    /// result indicates the migration has already been applied.
    /// </summary>
    SqlScript? IdempotencyCheckSql { get; }
}
