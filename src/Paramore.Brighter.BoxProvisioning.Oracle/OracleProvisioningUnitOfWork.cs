// The MIT License (MIT)
// Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace Paramore.Brighter.BoxProvisioning.Oracle;

/// <summary>
/// Oracle unit-of-work for box-table migrations. Oracle is the transactionless member of the
/// relational family for migration purposes — DDL implicitly commits the surrounding transaction,
/// so the UoW only acquires the session-level <c>DBMS_LOCK</c> that serialises concurrent
/// runners and never opens a <see cref="OracleTransaction"/>; the <see cref="Transaction"/>
/// property is always <c>null</c>.
/// </summary>
/// <remarks>
/// The class implements <see cref="IAmAProvisioningUnitOfWork{TTransaction}"/> with
/// <c>TTransaction = OracleTransaction</c> for symmetry with the other relational backends —
/// see the <see cref="IAmAProvisioningUnitOfWork{TTransaction}.Transaction"/> XML-doc which
/// documents the null-Transaction contract for transactionless backends.
/// </remarks>
public class OracleProvisioningUnitOfWork(
    OracleConnection connection,
    IOracleAdvisoryLock advisoryLock,
    ILogger logger,
    string? tableName = null) : IAmAProvisioningUnitOfWork<OracleTransaction>
{
    private string? _lockResource;

    /// <inheritdoc />
    /// <remarks>
    /// Always returns <c>null</c> — Oracle DDL auto-commits per statement.
    /// </remarks>
    public OracleTransaction? Transaction => null;

    /// <inheritdoc />
    public async Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        // Oracle DDL implicitly commits, so wrapping the migration run in a BEGIN/COMMIT yields
        // nothing useful. The UoW therefore only acquires the DBMS_LOCK advisory lock and stores
        // the lock resource for explicit RELEASE on Commit/Rollback. No transaction is opened.
        //
        // _lockResource is assigned ONLY AFTER AcquireAsync returns successfully. If Acquire
        // throws, _lockResource remains null so a defensive Rollback is a clean no-op via the
        // `if (_lockResource is null) return;` short-circuit in ReleaseLockAsync.
        logger.LogTrace("Beginning Oracle provisioning UoW for resource {LockResource}", lockResource);
        await advisoryLock.AcquireAsync(connection, lockResource, lockTimeout, cancellationToken);
        _lockResource = lockResource;
    }

    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken) =>
        ReleaseLockAsync(cancellationToken);

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken) =>
        ReleaseLockAsync(cancellationToken);

    private async Task ReleaseLockAsync(CancellationToken cancellationToken)
    {
        if (_lockResource is null) return;

        bool? releaseResult;
        try
        {
            releaseResult = await advisoryLock.ReleaseAsync(connection, _lockResource, cancellationToken);
        }
        catch (Exception ex)
        {
            // DBMS_LOCK.RELEASE executes SQL on the same connection. If the connection is dead,
            // ReleaseAsync throws. RollbackAsync MUST NOT throw — otherwise the runner's catch
            // path replaces the original migration exception with this cleanup-side failure.
            // The session-scoped DBMS_LOCK is released by the server when the connection closes.
            logger.LogWarning(
                ex,
                "Oracle provisioning UoW: DBMS_LOCK.RELEASE threw for lock resource '{LockResource}' — release skipped, the session will release the lock when the connection closes.",
                _lockResource);
            return;
        }

        if (releaseResult is true) return;

        var marker = releaseResult is null ? "null" : "false";
        var meaning = releaseResult is null ? "lock was never acquired" : "not the lock owner or parameter error";
        if (tableName is not null)
        {
            logger.LogWarning(
                "Oracle advisory lock for migration of '{TableName}' (key '{LockKey}') was not released by this session: DBMS_LOCK.RELEASE returned {Result} ({Marker} = {Meaning}). This is likely a Brighter defect — please report it.",
                tableName, _lockResource, releaseResult, marker, meaning);
        }
        else
        {
            logger.LogWarning(
                "Oracle advisory lock '{LockResource}' was not released by this session: DBMS_LOCK.RELEASE returned {Result} ({Marker} = {Meaning}). This is likely a Brighter defect — please report it.",
                _lockResource, releaseResult, marker, meaning);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => default;
}
