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
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning;

/// <summary>
/// The detection-helper role for a BoxProvisioning backend. Implementations
/// answer the questions the migration runner needs to decide whether a box
/// table exists, whether the migration history has been recorded, and which
/// columns are present on the live table.
/// </summary>
/// <remarks>
/// One implementation per relational backend (MSSQL, Postgres, MySQL, SQLite,
/// Spanner). Spanner's degenerate fresh-install-only model implements this
/// base interface only; the four relational backends additionally implement
/// <see cref="IAmAVersionDetectingMigrationHelper{TConnection,TTransaction}"/>
/// to participate in the bootstrap (legacy-table) branch.
/// <para>
/// Implementations are stateless services and are safe to register as DI
/// singletons. Methods accept an optional transaction so a single helper
/// instance can participate in either a transaction-bearing call site (e.g.
/// the relational migration runner during the atomic apply window) or a
/// transactionless probe (e.g. provisioner pre-check).
/// </para>
/// </remarks>
/// <typeparam name="TConnection">The backend-specific <see cref="DbConnection"/> subtype.</typeparam>
/// <typeparam name="TTransaction">The backend-specific <see cref="DbTransaction"/> subtype.
/// Backends that do not consume a transaction (MySQL — DDL auto-commits per ADR 0057 §5a;
/// Spanner — single-statement DDL) accept and ignore the parameter; the implementing class
/// states this on its XML-doc.</typeparam>
public interface IAmABoxMigrationDetectionHelper<TConnection, TTransaction>
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    /// <summary>
    /// Returns true if the named box table is present in the backend's catalogue.
    /// </summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="tableName">The unqualified table name to probe for.</param>
    /// <param name="schemaName">Optional. Null is substituted with the backend default by each
    /// implementation — see implementing class for the substitution rule
    /// (MSSQL → <c>"dbo"</c>, Postgres → <c>"public"</c>, MySQL → <c>connection.Database</c>;
    /// SQLite and Spanner accept and ignore the parameter).</param>
    /// <param name="cancellationToken">Optional cancellation.</param>
    /// <param name="transaction">Optional. The transaction to enrol the probe under, if the
    /// caller has an in-flight transaction. Null performs the probe outside any transaction.</param>
    Task<bool> DoesTableExistAsync(
        TConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);

    /// <summary>
    /// Returns true if a migration-history row is present for the named box table.
    /// </summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="tableName">The unqualified box table name whose history is being probed.</param>
    /// <param name="schemaName">Optional. Null is substituted with the backend default by each
    /// implementation — see <see cref="DoesTableExistAsync"/> for the substitution rule.</param>
    /// <param name="cancellationToken">Optional cancellation.</param>
    /// <param name="transaction">Optional. See <see cref="DoesTableExistAsync"/>.</param>
    Task<bool> DoesHistoryExistAsync(
        TConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);

    /// <summary>
    /// Returns the highest applied migration version recorded in the history table for
    /// the named box table, or zero if no rows are present.
    /// </summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="tableName">The unqualified box table name whose history is being read.</param>
    /// <param name="schemaName">Optional. Null is substituted with the backend default by each
    /// implementation — see <see cref="DoesTableExistAsync"/> for the substitution rule.</param>
    /// <param name="cancellationToken">Optional cancellation.</param>
    /// <param name="transaction">Optional. See <see cref="DoesTableExistAsync"/>.</param>
    Task<int> GetMaxVersionAsync(
        TConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);

    /// <summary>
    /// Returns the set of column names present on the named box table. Used by the
    /// runner's bootstrap branch to infer which V_k a legacy table sits at by comparing
    /// the live column set against each migration's expected column set.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="IReadOnlyCollection{T}"/> rather than <see cref="IReadOnlySet{T}"/>
    /// for symmetry with <see cref="IAmABoxMigration.LogicalColumns"/> and because
    /// <see cref="IReadOnlySet{T}"/> is not available on netstandard2.0.
    /// </remarks>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="tableName">The unqualified box table name whose columns are being read.</param>
    /// <param name="schemaName">Optional. Null is substituted with the backend default by each
    /// implementation — see <see cref="DoesTableExistAsync"/> for the substitution rule.</param>
    /// <param name="cancellationToken">Optional cancellation.</param>
    /// <param name="transaction">Optional. See <see cref="DoesTableExistAsync"/>.</param>
    Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
        TConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);

    /// <summary>
    /// Returns the discriminator value the runner records in the migration-history
    /// table to distinguish outbox migrations from inbox migrations for this backend.
    /// Pure function — no I/O.
    /// </summary>
    /// <param name="boxType">The box type whose discriminator is being requested.</param>
    string DiscriminatorFor(BoxType boxType);
}
