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
    MigrationVersion Version,
    MigrationDescription Description,
    SqlScript UpScript,
    IReadOnlyCollection<string> LogicalColumns,
    SourceReference? SourceReference = null,
    SqlScript? IdempotencyCheckSql = null) : IAmABoxMigration;
