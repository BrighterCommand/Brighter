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
/// transaction. The ctor therefore takes only the connection and a logger; there is no
/// <c>IAmA*AdvisoryLock</c> dependency to inject.
/// </summary>
/// <remarks>
/// Microsoft.Data.Sqlite's <c>BeginTransactionAsync</c> overload does not expose the
/// <c>deferred</c> flag, so the synchronous <see cref="SqliteConnection.BeginTransaction(IsolationLevel, bool)"/>
/// form is used to guarantee <c>BEGIN IMMEDIATE</c> (<c>deferred: false</c>). The wait under
/// writer-slot contention is bounded by <see cref="SqliteConnection.DefaultTimeout"/>, which
/// <see cref="SqliteBoxMigrationRunner"/> sets from the caller-supplied <c>lockTimeout</c>
/// before opening the connection. The driver translates DefaultTimeout into
/// sqlite3_busy_timeout on every internal statement (PRAGMA busy_timeout issued in SQL is
/// silently overwritten by the next command), so the retry-with-backoff under SQLITE_BUSY is
/// driver-side; if the budget expires the original <see cref="SqliteException"/> with
/// <c>SqliteErrorCode == 5</c> propagates to the runner's catch path. Per ADR 0057 §4 /
/// ADR 0058 §B.1 this replaces a separate advisory-lock primitive — BEGIN IMMEDIATE is itself
/// the lock acquisition.
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
    /// <see cref="SqliteException"/> with <c>SqliteErrorCode == 5</c> (<c>SQLITE_BUSY</c>) and
    /// propagates to the runner's catch path. See the class-level remarks for the driver
    /// quirk that makes DefaultTimeout the only reliable hook (PRAGMA busy_timeout is
    /// overwritten per-command).
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
    /// Commits the underlying transaction opened by <see cref="BeginAsync"/>. Per ADR 0058
    /// §B.1 SQLite has no advisory-lock primitive — the database-wide RESERVED writer slot
    /// reserved by the initial <c>BEGIN IMMEDIATE</c> is itself the migration lock — so
    /// committing the transaction is what releases the writer slot. There is no separate
    /// lock-release call. A no-op return when <c>_transaction</c> is null tolerates a
    /// Commit-without-Begin or a Begin-that-threw-before-opening-the-transaction.
    /// </remarks>
    public Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return Task.CompletedTask;
        return _transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Best-effort per ADR 0058 §B.3. The runner may invoke <see cref="RollbackAsync"/> from
    /// its catch path after a thrown <see cref="CommitAsync"/>, by which time the underlying
    /// transaction may already be finalised (committed-but-client-side-failed, or zombied by
    /// a closed connection); in those cases <see cref="SqliteTransaction.Rollback"/> throws
    /// <see cref="InvalidOperationException"/> ("Transaction has completed; it is no longer
    /// usable."). A zombied connection or async cancellation can surface as
    /// <see cref="ObjectDisposedException"/> / <see cref="OperationCanceledException"/> too.
    /// <see cref="RollbackAsync"/> MUST NOT throw for any of these — the runner's catch path
    /// (<c>catch { uow.RollbackAsync(...); throw; }</c>) cannot have its primary migration
    /// exception masked by a cleanup-side failure. Catch <see cref="Exception"/> (matching the
    /// MSSQL UoW stance at MsSqlProvisioningUnitOfWork.cs:108 and the PG UoW stance at
    /// PostgreSqlProvisioningUnitOfWork.cs:165) and log a Warning so the unwind continues.
    /// SQLite has no advisory-lock primitive to release separately; completing the transaction
    /// (or a zombied tx being disposed) releases the writer slot regardless of rollback success.
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
