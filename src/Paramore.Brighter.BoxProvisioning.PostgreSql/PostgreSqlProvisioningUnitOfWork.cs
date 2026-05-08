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
        await _advisoryLock.AcquireAsync(_connection, lockResource, lockTimeout, cancellationToken);
        _transaction = (NpgsqlTransaction)await _connection.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken)
    {
        // Phase 5.2.a: minimum no-op. Phase 5.2.b refines this to commit the transaction and
        // explicitly release the advisory lock via pg_advisory_unlock per ADR 0058 §B.1.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        // Phase 5.2.a: minimum no-op. Phase 5.2.c refines this to roll back the transaction
        // and release the advisory lock — best-effort on both per ADR 0058 §B.3.
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_transaction is null) return default;
        return _transaction.DisposeAsync();
    }
}
