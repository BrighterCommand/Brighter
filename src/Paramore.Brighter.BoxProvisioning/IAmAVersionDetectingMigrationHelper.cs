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
/// The detection-helper role for relational backends that participate in the
/// bootstrap (legacy-table) branch of the migration runner. Extends
/// <see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}"/> with
/// the ability to infer which V_k a pre-existing table without history sits at.
/// </summary>
/// <remarks>
/// Implemented by the four relational backends (MSSQL, Postgres, MySQL, SQLite).
/// <para>
/// Spanner does NOT implement this interface. Per ADR 0057 §6, Spanner's
/// degenerate fresh-install-only model has no V_k chain to detect — its
/// detection helper implements the base
/// <see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}"/>
/// only, and the Spanner runner skips the bootstrap branch entirely.
/// </para>
/// </remarks>
/// <typeparam name="TConnection">The backend-specific <see cref="DbConnection"/> subtype.</typeparam>
/// <typeparam name="TTransaction">The backend-specific <see cref="DbTransaction"/> subtype.
/// Backends that do not consume a transaction accept and ignore the parameter; see the
/// implementing class's XML-doc.</typeparam>
public interface IAmAVersionDetectingMigrationHelper<TConnection, TTransaction> :
    IAmABoxMigrationDetectionHelper<TConnection, TTransaction>
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    /// <summary>
    /// Infers the migration version of an existing box table that has no history rows
    /// (a legacy table installed before history tracking was introduced) by comparing
    /// the live column set against each migration's expected column set, returning
    /// the highest version whose column set is a subset of the live columns.
    /// </summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="tableName">The unqualified box table name whose version is being inferred.</param>
    /// <param name="schemaName">Optional. Null is substituted with the backend default by each
    /// implementation — see
    /// <see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}.DoesTableExistAsync"/>
    /// for the substitution rule.</param>
    /// <param name="boxType">The box type whose migration chain is being matched against.</param>
    /// <param name="migrations">The migration chain in monotonic version order, as produced by
    /// <see cref="IAmABoxMigrationCatalog.All"/>.</param>
    /// <param name="cancellationToken">Optional cancellation.</param>
    /// <param name="transaction">Optional. See
    /// <see cref="IAmABoxMigrationDetectionHelper{TConnection,TTransaction}.DoesTableExistAsync"/>.</param>
    Task<int> DetectCurrentVersionAsync(
        TConnection connection, string tableName, string? schemaName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken = default,
        TTransaction? transaction = null);
}
