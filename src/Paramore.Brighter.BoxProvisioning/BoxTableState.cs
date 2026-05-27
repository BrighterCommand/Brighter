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

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Describes the pre-lock state of a box table in the database, as observed by
/// <see cref="SqlBoxProvisioner{TConnection,TTransaction}"/> / Spanner provisioner before
/// invoking <see cref="IAmABoxMigrationRunner.MigrateAsync"/>.
/// </summary>
/// <remarks>
/// Consumed asymmetrically across backends:
/// <list type="bullet">
/// <item><description>The four relational runners
/// (<see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/>) re-detect their state
/// under the advisory-lock-bearing UoW (ADR 0057 §3 TOCTOU defence) and so discard the
/// <see cref="TableExists"/> / <see cref="HistoryExists"/> / <see cref="CurrentVersion"/>
/// fields. The record is passed only to keep the interface uniform across backends.</description></item>
/// <item><description>The Spanner runner has no advisory-lock concept (ADR 0057 §6) and reads
/// <see cref="CurrentVersion"/> directly during the normal-update path to compare against
/// <c>V_latest</c>.</description></item>
/// </list>
/// </remarks>
/// <param name="TableExists">Whether the box table exists in the database.</param>
/// <param name="HistoryExists">Whether the migration history table exists and has entries for this box.</param>
/// <param name="CurrentVersion">The current schema version of the box table, clamped to ≥ 0
/// (the MySQL F11 fix). Spanner reads this on the normal-update path; relational runners
/// discard it and re-read the recorded version via
/// <c>IAmABoxMigrationDetectionHelper.GetMaxVersionAsync</c> inside the UoW.</param>
public record BoxTableState(bool TableExists, bool HistoryExists, int CurrentVersion);
