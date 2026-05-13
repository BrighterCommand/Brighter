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
/// form is used to guarantee <c>BEGIN IMMEDIATE</c> (<c>deferred: false</c>). The call is
/// local-only and fast — any wait for a contended writer slot manifests as
/// <c>SQLITE_BUSY</c> being returned from this call and surfaces to the runner's catch path.
/// Concurrent provisioning against the same SQLite file is fail-fast by design: neither the
/// UoW nor the runner retries (the runner's <c>PRAGMA busy_timeout = 0</c> deliberately
/// suppresses the inherited driver wait — see ADR 0057 §4 / ADR 0058 §B.1). Typical
/// single-host deployments will never see contention; multi-process scenarios (sidecar +
/// main app each calling <c>UseBoxProvisioning</c>) should serialise startup externally.
/// </remarks>
public class SqliteProvisioningUnitOfWork(
    SqliteConnection connection,
    ILogger logger) : IAmAProvisioningUnitOfWork<SqliteTransaction>
{
    private readonly SqliteConnection _connection = connection;
    private readonly ILogger _logger = logger;
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
    /// The <paramref name="lockTimeout"/> parameter is accepted for interface symmetry but is
    /// not honoured: if the writer slot is contended, <c>BEGIN IMMEDIATE</c> throws
    /// <see cref="SqliteException"/> with <c>SqliteErrorCode == 5</c> (<c>SQLITE_BUSY</c>)
    /// immediately and propagates to the runner's catch path. Concurrent SQLite migration is
    /// fail-fast by design — see the class-level remarks and ADR 0057 §4 / ADR 0058 §B.1.
    /// </para>
    /// </remarks>
    public Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogTrace("Beginning SQLite provisioning UoW for resource {LockResource}", lockResource);
        _transaction = _connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
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
    /// usable."). <see cref="RollbackAsync"/> MUST NOT throw — log a Warning and return so the
    /// runner's unwind continues. SQLite has no advisory-lock primitive to release separately;
    /// completing the transaction (or a zombied tx being disposed) releases the writer slot.
    /// </remarks>
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return;
        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "SQLite provisioning UoW: rollback skipped — transaction already finalised");
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_transaction is null) return default;
        return _transaction.DisposeAsync();
    }
}
