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
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.BoxProvisioning.MsSql;

/// <summary>
/// SQL Server unit-of-work for box-table migrations. Coordinates the per-run pairing of an
/// open <see cref="SqlTransaction"/> and an <c>sp_getapplock</c>-backed advisory lock; per
/// ADR 0058 §B.1 the lock is acquired with <c>@LockOwner='Transaction'</c> so it is bound
/// to the surrounding transaction's lifetime and released implicitly on commit/rollback.
/// </summary>
public class MsSqlProvisioningUnitOfWork(
    SqlConnection connection,
    IMsSqlAdvisoryLock advisoryLock,
    ILogger logger) : IAmAProvisioningUnitOfWork<SqlTransaction>
{
    private SqlTransaction? _transaction;

    /// <inheritdoc />
    public SqlTransaction? Transaction => _transaction;

    /// <inheritdoc />
    public async Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        logger.LogTrace("Beginning MSSQL provisioning UoW for resource {LockResource}", lockResource);
#if NET8_0_OR_GREATER
        _transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
#else
        _transaction = connection.BeginTransaction();
#endif
        await advisoryLock.AcquireAsync(
            connection, _transaction, lockResource, lockTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return Task.CompletedTask;
#if NET8_0_OR_GREATER
        return _transaction.CommitAsync(cancellationToken);
#else
        _transaction.Commit();
        return Task.CompletedTask;
#endif
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null) return;
        try
        {
#if NET8_0_OR_GREATER
            await _transaction.RollbackAsync(cancellationToken);
#else
            _transaction.Rollback();
            await Task.CompletedTask;
#endif
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "MSSQL provisioning UoW: rollback skipped — transaction already finalised or unwind failed (lock resource bound to transaction lifetime; SQL Server releases sp_getapplock on transaction completion regardless)");
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_transaction is null) return default;
#if NET8_0_OR_GREATER
        return _transaction.DisposeAsync();
#else
        _transaction.Dispose();
        return default;
#endif
    }
}
