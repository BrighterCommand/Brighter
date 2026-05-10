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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Paramore.Brighter.BoxProvisioning.PostgreSql;

/// <summary>
/// PostgreSQL unit-of-work for box-table migrations. Coordinates the per-run pairing of an
/// open <see cref="NpgsqlTransaction"/> and a <c>pg_advisory_lock</c>-backed advisory lock;
/// per ADR 0058 §B.1 the lock is session-scoped, so it is acquired BEFORE the transaction
/// begins and released explicitly via <c>pg_advisory_unlock</c> on commit or rollback (NOT
/// implicitly on transaction completion, which is the MSSQL pattern).
/// </summary>
public class PostgreSqlProvisioningUnitOfWork(
    NpgsqlConnection connection,
    IPostgreSqlAdvisoryLock advisoryLock,
    ILogger logger) : IAmAProvisioningUnitOfWork<NpgsqlTransaction>
{
    private readonly NpgsqlConnection _connection = connection;
    private readonly IPostgreSqlAdvisoryLock _advisoryLock = advisoryLock;
    private readonly ILogger _logger = logger;
    private NpgsqlTransaction? _transaction;
    private string? _lockResource;

    /// <inheritdoc />
    public NpgsqlTransaction? Transaction => _transaction;

    /// <inheritdoc />
    public async Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        // Per ADR 0058 §B.1: the advisory lock MUST be acquired before the transaction is
        // opened because pg_advisory_lock is session-scoped — the lock outlives any
        // transaction on the connection and is released either by an explicit
        // pg_advisory_unlock or when the connection closes. Acquiring the lock outside any
        // transaction makes the contention window explicit and decouples the lock's lifetime
        // from any in-flight tx. Reverse ordering would still acquire a session-scoped lock
        // (Postgres permits advisory-lock acquisition inside a tx), but the sequencing would
        // be misleading and breaks the §B.1 contract.
        _logger.LogTrace("Beginning Postgres provisioning UoW for resource {LockResource}", lockResource);
        _lockResource = lockResource;
        await _advisoryLock.AcquireAsync(_connection, lockResource, lockTimeout, cancellationToken);
        _transaction = (NpgsqlTransaction)await _connection.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        // Per ADR 0058 §B.1: lock release is EXPLICIT for Postgres because pg_advisory_lock is
        // session-scoped — it is NOT auto-released on transaction commit (the MSSQL pattern,
        // where sp_getapplock with @LockOwner='Transaction' rides the tx lifetime, does not
        // apply here). Commit the tx first, then call pg_advisory_unlock; both must succeed
        // in this happy-path branch.
        if (_transaction is null) return;
        await _transaction.CommitAsync(cancellationToken);
        var held = await _advisoryLock.ReleaseAsync(_connection, _lockResource!, cancellationToken);
        if (!held)
        {
            // Per ADR 0057 §5b: pg_advisory_unlock returns false when this session does not
            // currently hold the named lock — a diagnostic anomaly because the UoW just
            // acquired it. Surface it as a Warning naming the lock resource so the operator
            // can correlate against the runner's table-level context, and continue.
            _logger.LogWarning(
                "Postgres provisioning UoW: pg_advisory_unlock returned false for lock resource '{LockResource}' — the lock was not held by this session at release. This is likely a Brighter defect — please report it.",
                _lockResource);
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        // Per ADR 0058 §B.3: best-effort. The runner may call RollbackAsync after a thrown
        // CommitAsync (the transaction may already be in a finalised state by then —
        // committed-but-client-side-failed, or zombied by a broken connection); calling
        // _transaction.RollbackAsync on a finalised tx throws InvalidOperationException
        // ("Cannot rollback; this transaction has been completed..."). RollbackAsync MUST NOT
        // throw — log a Warning and continue so the lock is still released. Disposal-style
        // semantics: BOTH halves of the unwind are attempted regardless.
        if (_transaction is not null)
        {
            try
            {
                await _transaction.RollbackAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Postgres provisioning UoW: rollback skipped — transaction already finalised");
            }
        }
        if (_lockResource is not null)
        {
            var held = await _advisoryLock.ReleaseAsync(_connection, _lockResource, cancellationToken);
            if (!held)
            {
                _logger.LogWarning(
                    "Postgres provisioning UoW: pg_advisory_unlock returned false for lock resource '{LockResource}' — the lock was not held by this session at release. This is likely a Brighter defect — please report it.",
                    _lockResource);
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_transaction is null) return default;
        return _transaction.DisposeAsync();
    }
}
