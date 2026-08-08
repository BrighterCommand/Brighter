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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// Knows how to run migration steps against a database and track which
/// versions have been applied.
/// </summary>
/// <remarks>
/// Spec 0027 R1 (PR #4039 part 2): the runner retrieves its migration chain and
/// fresh-install DDL from an injected <see cref="IAmABoxMigrationCatalog"/>, so the
/// caller no longer threads the migration list through every invocation. The Spanner
/// runner is exempt from <see cref="IAmABoxMigrationCatalog"/> per ADR 0057 §6 and
/// implements this interface directly with its own per-backend fresh-install logic.
/// </remarks>
public interface IAmABoxMigrationRunner
{
    /// <summary>
    /// Apply all outstanding migrations for the specified box table.
    /// </summary>
    /// <param name="tableName">The box table name.</param>
    /// <param name="schemaName">The database schema name (e.g. "dbo" for MSSQL).</param>
    /// <param name="boxType">The type of box being migrated; used to pick the discriminator column for under-lock re-detection.</param>
    /// <param name="tableState">The pre-lock state of the box table. Consumption is
    /// asymmetric across backends and intentionally so: the four relational runners
    /// (<see cref="SqlBoxMigrationRunner{TConnection,TTransaction}"/>) treat the entire
    /// record as a hint and discard it — re-detection under the advisory-lock-bearing UoW
    /// supplies the authoritative state (ADR 0057 §3 TOCTOU defence). The Spanner runner,
    /// which has no advisory-lock concept (ADR 0057 §6), reads
    /// <see cref="BoxTableState.CurrentVersion"/> directly to decide the normal-update path.
    /// See <see cref="BoxTableState"/> for the per-field disposition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MigrateAsync(
        BoxTableName tableName,
        SchemaName? schemaName,
        BoxType boxType,
        BoxTableState tableState,
        CancellationToken cancellationToken = default);
}
