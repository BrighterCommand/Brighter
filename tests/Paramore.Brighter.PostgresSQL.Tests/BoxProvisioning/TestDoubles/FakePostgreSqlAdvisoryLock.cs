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
/// Test double for <see cref="IPostgreSqlAdvisoryLock"/>: <see cref="AcquireAsync"/> is a
/// no-op success that records the lock key it was called with (or throws the configured
/// exception); <see cref="ReleaseAsync"/> returns the parameterised <see cref="bool"/> so
/// tests can drive the runner's diagnostic path for non-true unlock results.
/// </summary>
/// <remarks>
/// The optional <c>senseTransactionStateAtAcquire</c> flag makes <see cref="AcquireAsync"/>
/// additionally probe whether a transaction was already active on the connection at the time
/// it was invoked, by attempting <see cref="NpgsqlConnection.BeginTransactionAsync(CancellationToken)"/>
/// inside a try/catch. Npgsql throws <see cref="InvalidOperationException"/> ("A transaction
/// is already in progress; nested/concurrent transactions aren't supported.") when a tx is
/// already active — used by the UoW BeginAsync ordering test (ADR 0058 §B.1) to verify that
/// AcquireAsync runs BEFORE the UoW's BeginTransactionAsync.
/// The optional <c>throwOnAcquire</c> exception drives the runner's distinguishable-exception-
/// propagation path per ADR 0058 §B.3 (lock-acquisition failure surfaces unwrapped to the
/// caller; the UoW's <c>await using</c> scope then exercises the partial-init DisposeAsync
/// contract). Mirrors <c>FakeMsSqlAdvisoryLock(throwOnAcquire)</c>.
/// </remarks>
internal sealed class FakePostgreSqlAdvisoryLock(
    bool releaseResult,
    bool senseTransactionStateAtAcquire = false,
    Exception? throwOnAcquire = null,
    Exception? throwOnRelease = null) : IPostgreSqlAdvisoryLock
{
    public string? AcquiredKey { get; private set; }
    public string? ReleasedKey { get; private set; }
    public TimeSpan? AcquiredTimeout { get; private set; }

    /// <summary>
    /// Captured at the moment <see cref="AcquireAsync"/> was first invoked, if
    /// <c>senseTransactionStateAtAcquire</c> was true. <c>null</c> until first invocation;
    /// <c>false</c> if no transaction was active on the connection at the time of the call;
    /// <c>true</c> if a transaction was already active (Npgsql refused to begin a probe tx).
    /// </summary>
    public bool? TransactionWasActiveAtAcquireTime { get; private set; }

    public async Task AcquireAsync(
        NpgsqlConnection connection, string lockKey,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (senseTransactionStateAtAcquire)
        {
            try
            {
                await using var probe = await connection.BeginTransactionAsync(cancellationToken);
                TransactionWasActiveAtAcquireTime = false;
            }
            catch (InvalidOperationException)
            {
                TransactionWasActiveAtAcquireTime = true;
            }
        }
        AcquiredKey = lockKey;
        AcquiredTimeout = timeout;
        if (throwOnAcquire is not null) throw throwOnAcquire;
    }

    public Task<bool> ReleaseAsync(
        NpgsqlConnection connection, string lockKey,
        CancellationToken cancellationToken)
    {
        ReleasedKey = lockKey;
        if (throwOnRelease is not null) throw throwOnRelease;
        return Task.FromResult(releaseResult);
    }
}
