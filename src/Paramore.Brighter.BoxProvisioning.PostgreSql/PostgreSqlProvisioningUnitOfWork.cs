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
    private NpgsqlTransaction? _transaction;
    private string? _lockResource;

    /// <inheritdoc />
    public NpgsqlTransaction? Transaction => _transaction;

    /// <inheritdoc />
    public async Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        logger.LogTrace("Beginning Postgres provisioning UoW for resource {LockResource}", lockResource);
        _lockResource = lockResource;
        await advisoryLock.AcquireAsync(connection, lockResource, lockTimeout, cancellationToken);
        try
        {
            _transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        }
        catch
        {
            // Per ADR 0058 §B.3 atomic-Begin: BeginTransactionAsync threw AFTER the session-
            // scoped lock was acquired. The §B.3 runner contract places BeginAsync OUTSIDE
            // its try/catch (pinned by When_relational_box_migration_runner_base_begin_async_throws...),
            // so RollbackAsync will not run — the lock would leak until connection close
            // (bounded but messy). Release the lock at source so atomic-Begin holds: a
            // BeginAsync that throws guarantees no resources are held. _lockResource is
            // cleared so a subsequent best-effort RollbackAsync is a clean no-op.
            try
            {
                // Pass CancellationToken.None — NOT the caller's token — mirroring §B.3
                // rollback semantics: if BeginTransactionAsync threw because the caller's
                // token was cancelled (or cancelled during this catch block), reusing that
                // token here short-circuits pg_advisory_unlock with OperationCanceledException
                // before the SQL is dispatched, and the session-scoped lock leaks to
                // connection close. The release SQL is a single round-trip and bounded by the
                // connection state; running it untokened guarantees the unlock attempt reaches
                // the server even when the caller has already abandoned the operation.
                await advisoryLock.ReleaseAsync(connection, lockResource, CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                // Release-on-cleanup MUST NOT replace the original BeginTransactionAsync
                // exception. Log + swallow; the session-scoped lock is released by the server
                // when the connection closes.
                logger.LogWarning(
                    releaseEx,
                    "Postgres provisioning UoW: pg_advisory_unlock threw while cleaning up after BeginTransactionAsync failure for lock resource '{LockResource}' — release skipped, the session will release the lock when the connection closes.",
                    lockResource);
            }
            _lockResource = null;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return;
        await _transaction.CommitAsync(cancellationToken);
        var held = await advisoryLock.ReleaseAsync(connection, _lockResource!, cancellationToken);
        if (!held)
        {
            logger.LogWarning(
                "Postgres provisioning UoW: pg_advisory_unlock returned false for lock resource '{LockResource}' — the lock was not held by this session at release. This is likely a Brighter defect — please report it.",
                _lockResource);
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            try
            {
                await _transaction.RollbackAsync(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(
                    ex,
                    "Postgres provisioning UoW: rollback skipped — transaction already finalised");
            }
        }
        if (_lockResource is not null)
        {
            try
            {
                var held = await advisoryLock.ReleaseAsync(connection, _lockResource, cancellationToken);
                if (!held)
                {
                    logger.LogWarning(
                        "Postgres provisioning UoW: pg_advisory_unlock returned false for lock resource '{LockResource}' — the lock was not held by this session at release. This is likely a Brighter defect — please report it.",
                        _lockResource);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Postgres provisioning UoW: pg_advisory_unlock threw for lock resource '{LockResource}' — release skipped, the session will release the lock when the connection closes.",
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
