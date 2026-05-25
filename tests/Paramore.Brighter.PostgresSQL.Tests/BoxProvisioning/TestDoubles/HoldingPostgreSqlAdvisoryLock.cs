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

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.TestDoubles;

/// <summary>
/// Test decorator over <see cref="IPostgreSqlAdvisoryLock"/>: delegates Acquire to the
/// underlying implementation, then signals <see cref="AcquireSeen"/> and parks until the
/// caller-supplied <c>releaseGate</c> task completes. While parked, the underlying real
/// Postgres advisory lock is genuinely held — any concurrent acquire of the same lock key
/// on a separate session will block (or time out).
/// </summary>
internal sealed class HoldingPostgreSqlAdvisoryLock(
    IPostgreSqlAdvisoryLock inner,
    Task releaseGate) : IPostgreSqlAdvisoryLock
{
    public TaskCompletionSource<bool> AcquireSeen { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task AcquireAsync(
        NpgsqlConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        await inner.AcquireAsync(connection, lockKey, timeout, cancellationToken);
        AcquireSeen.TrySetResult(true);
        await releaseGate.WaitAsync(cancellationToken);
    }

    public Task<bool> ReleaseAsync(
        NpgsqlConnection connection, string lockKey,
        CancellationToken cancellationToken)
        => inner.ReleaseAsync(connection, lockKey, cancellationToken);
}
