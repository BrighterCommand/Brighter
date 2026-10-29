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

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.BoxProvisioning.Sqlite;

/// <summary>
/// SQLite unit-of-work for box-table migrations. Per ADR 0057 §4 / ADR 0058 §B.1 SQLite has no
/// advisory-lock primitive — the database-wide RESERVED writer slot acquired by
/// <c>BEGIN IMMEDIATE</c> is itself the migration lock, combined atomically with opening the
/// transaction.
/// </summary>
/// <remarks>
/// Microsoft.Data.Sqlite's <c>BeginTransactionAsync</c> overload does not expose the
/// <c>deferred</c> flag, so the synchronous <see cref="SqliteConnection.BeginTransaction(IsolationLevel, bool)"/>
/// form is used to guarantee <c>BEGIN IMMEDIATE</c>. The wait under writer-slot contention is
/// bounded by <see cref="SqliteConnection.DefaultTimeout"/>, which
/// <see cref="SqliteBoxMigrationRunner"/> sets from the caller-supplied <c>lockTimeout</c>.
/// </remarks>
public class SqliteProvisioningUnitOfWork(
    SqliteConnection connection,
    ILogger logger) : IAmAProvisioningUnitOfWork<SqliteTransaction>
{
    private SqliteTransaction? _transaction;

    /// <inheritdoc />
    public SqliteTransaction? Transaction => _transaction;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Issues <c>BEGIN IMMEDIATE</c> on the connection, atomically reserving SQLite's
    /// database-wide writer slot and opening the surrounding transaction. The
    /// <paramref name="lockResource"/> parameter is accepted for interface symmetry with the
    /// other relational backends and is logged for diagnostic continuity, but is otherwise
    /// unused — SQLite has no named-lock primitive (per ADR 0057 §4).
    /// </para>
    /// <para>
    /// The <paramref name="lockTimeout"/> parameter is honoured indirectly: the runner
    /// translates it into <see cref="SqliteConnection.DefaultTimeout"/> on the connection
    /// before this call, which the driver applies as sqlite3_busy_timeout on the synthesised
    /// <c>BEGIN IMMEDIATE</c>. If the slot remains held past the budget the call throws
    /// <see cref="SqliteException"/> with <c>SqliteErrorCode == 5</c> (<c>SQLITE_BUSY</c>).
    /// </para>
    /// </remarks>
    public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogTrace("Beginning SQLite provisioning UoW for resource {LockResource}", lockResource);
        _transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Commits the transaction opened by <see cref="BeginAsync"/>, which releases the writer
    /// slot. A no-op if <see cref="BeginAsync"/> did not open a transaction.
    /// </remarks>
    public Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return Task.CompletedTask;
        return _transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Best-effort per ADR 0058 §B.3: never throws, so a failed rollback cannot mask the
    /// migration exception that caused it. The runner may roll back after a failed
    /// <see cref="CommitAsync"/>, when the transaction is already finalised; the failure is
    /// logged as a warning. The writer slot is released when the transaction completes,
    /// whether or not the rollback succeeds.
    /// </remarks>
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return;
        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "SQLite provisioning UoW: rollback skipped — transaction already finalised or unwind failed (writer slot released on transaction completion regardless of rollback success)");
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_transaction is null) return default;
        return _transaction.DisposeAsync();
    }
}
