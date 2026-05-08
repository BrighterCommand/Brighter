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
    ILogger logger) : IAmAProvisioningUnitOfWork<MySqlTransaction>
{
    private readonly MySqlConnection _connection = connection;
    private readonly IMySqlAdvisoryLock _advisoryLock = advisoryLock;
    private readonly ILogger _logger = logger;
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
        _logger.LogTrace("Beginning MySQL provisioning UoW for resource {LockResource}", lockResource);
        _lockResource = lockResource;
        await _advisoryLock.AcquireAsync(_connection, lockResource, lockTimeout, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        // Per ADR 0058 §B.1 / ADR 0057 §5a / §5b: there is no transaction to commit (the
        // UoW never opens one — see BeginAsync). CommitAsync's only side-effect is to
        // release the session-level GET_LOCK acquired in BeginAsync, freeing contention
        // for the next runner. Tri-state RELEASE_LOCK diagnostic (NULL/0/1) Warning
        // logging is shared with RollbackAsync — landing in 5.3.c.
        if (_lockResource is null) return;
        await _advisoryLock.ReleaseAsync(_connection, _lockResource, cancellationToken);
    }

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => default;
}
