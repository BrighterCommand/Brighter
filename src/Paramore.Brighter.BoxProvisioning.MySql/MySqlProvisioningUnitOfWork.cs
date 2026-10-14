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
using MySqlConnector;

namespace Paramore.Brighter.BoxProvisioning.MySql;

/// <summary>
/// MySQL unit-of-work for box-table migrations. Per ADR 0057 §5a MySQL is the transactionless
/// member of the relational family — DDL implicitly commits the surrounding transaction, so
/// the UoW only acquires the session-level <c>GET_LOCK</c> that serialises concurrent runners
/// and never opens a <see cref="MySqlTransaction"/>; the <see cref="Transaction"/> property is
/// always <c>null</c>.
/// </summary>
/// <remarks>
/// The class instantiates <see cref="IAmAProvisioningUnitOfWork{TTransaction}"/> with
/// <c>TTransaction = MySqlTransaction</c> for symmetry with the other relational backends —
/// see the <see cref="IAmAProvisioningUnitOfWork{TTransaction}.Transaction"/> XML-doc which
/// documents the null-Transaction contract for transactionless backends.
/// </remarks>
public class MySqlProvisioningUnitOfWork(
    MySqlConnection connection,
    IMySqlAdvisoryLock advisoryLock,
    ILogger logger,
    string? tableName = null) : IAmAProvisioningUnitOfWork<MySqlTransaction>
{
    private string? _lockResource;

    /// <inheritdoc />
    /// <remarks>
    /// Always returns <c>null</c> — MySQL is transactionless per ADR 0057 §5a.
    /// </remarks>
    public MySqlTransaction? Transaction => null;

    /// <inheritdoc />
    public async Task BeginAsync(string lockResource, TimeSpan lockTimeout, CancellationToken cancellationToken)
    {
        logger.LogTrace("Beginning MySQL provisioning UoW for resource {LockResource}", lockResource);
        // Store _lockResource only after the acquire succeeds, so a failed Begin leaves nothing to release.
        await advisoryLock.AcquireAsync(connection, lockResource, lockTimeout, cancellationToken);
        _lockResource = lockResource;
    }

    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken) =>
        ReleaseLockAndLogTriStateAsync(cancellationToken);

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken) =>
        ReleaseLockAndLogTriStateAsync(cancellationToken);

    // Does nothing if Begin never acquired the lock. Never throws: the runner calls RollbackAsync
    // from its catch path, and a throw here would hide the migration's own exception; the server
    // releases the lock when the connection closes. RELEASE_LOCK returning 0 (held by another
    // session) or NULL (no such lock) cannot happen after our own acquire, so both are logged.
    private async Task ReleaseLockAndLogTriStateAsync(CancellationToken cancellationToken)
    {
        if (_lockResource is null) return;

        bool? releaseResult;
        try
        {
            releaseResult = await advisoryLock.ReleaseAsync(connection, _lockResource, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "MySQL provisioning UoW: RELEASE_LOCK threw for lock resource '{LockResource}' — release skipped, the session will release the lock when the connection closes.",
                _lockResource);
            return;
        }

        if (releaseResult is true) return;

        var marker = releaseResult is null ? "NULL" : "0";
        var meaning = releaseResult is null ? "lock did not exist" : "lock held by another session";
        if (tableName is not null)
        {
            // Phase 5.3 regression fix: restore the legacy runner's TableName+LockKey emission
            // shape per NF1 (no information loss). MySqlMigrationLockName.For hashes long
            // composites into a 64-char-safe GET_LOCK key, so lockResource alone cannot
            // surface the raw table name in long-name cases. The runner threads tableName
            // through the UoW ctor so this Warning preserves diagnostic context regardless
            // of name length.
            logger.LogWarning(
                "MySQL advisory lock for migration of '{TableName}' (key '{LockKey}') was not released by this session: RELEASE_LOCK returned {Result} ({Marker} = {Meaning}). This is likely a Brighter defect — please report it.",
                tableName, _lockResource, releaseResult, marker, meaning);
        }
        else
        {
            logger.LogWarning(
                "MySQL advisory lock '{LockResource}' was not released by this session: RELEASE_LOCK returned {Result} ({Marker} = {Meaning}). This is likely a Brighter defect — please report it.",
                _lockResource, releaseResult, marker, meaning);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => default;
}
