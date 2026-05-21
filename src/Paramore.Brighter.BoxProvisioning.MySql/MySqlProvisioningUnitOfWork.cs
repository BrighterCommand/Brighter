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
        // Per ADR 0058 §B.1 / ADR 0057 §5a: MySQL DDL implicitly commits, so wrapping the
        // migration run in a BEGIN/COMMIT yields nothing useful. The UoW therefore only
        // acquires the session-level GET_LOCK and stores the lock resource for explicit
        // RELEASE_LOCK on Commit/Rollback (Phase 5.3.b/5.3.c). No transaction is opened.
        //
        // _lockResource is assigned ONLY AFTER AcquireAsync returns successfully — PR #4039
        // reviewer fix F2 (item M2-3). If Acquire throws, _lockResource remains null so a
        // defensive direct Rollback (e.g. from a future runner change) is a clean no-op via
        // the `if (_lockResource is null) return;` short-circuit in ReleaseLockAndLogTriStateAsync.
        // Releasing a never-acquired lock would return NULL and emit a misleading
        // "Brighter defect" Warning masking the real lock-acquisition failure.
        logger.LogTrace("Beginning MySQL provisioning UoW for resource {LockResource}", lockResource);
        await advisoryLock.AcquireAsync(connection, lockResource, lockTimeout, cancellationToken);
        _lockResource = lockResource;
    }

    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken) =>
        // Per ADR 0058 §B.1 / ADR 0057 §5a / §5b: there is no transaction to commit (the
        // UoW never opens one — see BeginAsync). CommitAsync's only side-effect is to
        // release the session-level GET_LOCK acquired in BeginAsync. The tri-state
        // RELEASE_LOCK diagnostic is shared with RollbackAsync — see
        // ReleaseLockAndLogTriStateAsync.
        ReleaseLockAndLogTriStateAsync(cancellationToken);

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken) =>
        // Per ADR 0058 §B.3: disposal-style semantics — RollbackAsync MUST NOT throw. There
        // is no transaction to roll back (MySQL is transactionless per ADR 0057 §5a), so
        // the only obligation is to release the session-level GET_LOCK acquired in
        // BeginAsync. The tri-state RELEASE_LOCK diagnostic (NULL/0/1) Warning logging is
        // shared with CommitAsync — see ReleaseLockAndLogTriStateAsync.
        ReleaseLockAndLogTriStateAsync(cancellationToken);

    private async Task ReleaseLockAndLogTriStateAsync(CancellationToken cancellationToken)
    {
        // Per spec 0027 Item M / ADR 0057 §5b / ADR 0058 §B.3: MySQL's RELEASE_LOCK has
        // three outcomes — 1 (released by this session: clean), 0 (lock exists but held by
        // another session: diagnostic anomaly because we just acquired it), NULL (no lock
        // by that name: same anomaly because acquisition implied creation). Both non-true
        // outcomes surface through a Warning-level entry preserving the marker convention
        // (NULL/0) and the meaning text from the existing runner emission at
        // MySqlBoxMigrationRunner.cs:138-141 — NF1 / no-information-loss.
        //
        // Guarded by the `_lockResource is null` short-circuit so a Commit/Rollback called
        // without a prior BeginAsync (or after Begin threw before storing the lock
        // resource) is a clean no-op. This is what makes RollbackAsync safe to call from
        // the runner's catch path even if BeginAsync failed.
        if (_lockResource is null) return;

        bool? releaseResult;
        try
        {
            releaseResult = await advisoryLock.ReleaseAsync(connection, _lockResource, cancellationToken);
        }
        catch (Exception ex)
        {
            // RELEASE_LOCK executes SQL on the same connection — if the connection is dead
            // (mid-migration driver fault, ObjectDisposedException, MySqlException) ReleaseAsync
            // throws. RollbackAsync MUST NOT throw — otherwise the runner's catch path
            // (catch { RollbackAsync(...); throw; }) replaces the original migration exception
            // with this cleanup-side failure, masking the real cause. The session-scoped
            // GET_LOCK is released by the server when the connection closes.
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
