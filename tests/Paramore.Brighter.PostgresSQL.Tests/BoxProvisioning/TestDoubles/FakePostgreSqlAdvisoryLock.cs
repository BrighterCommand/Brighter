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

// senseTransactionStateAtAcquire probes by beginning a transaction: Npgsql throws InvalidOperationException when one is already active.
internal sealed class FakePostgreSqlAdvisoryLock(
    bool releaseResult,
    bool senseTransactionStateAtAcquire = false,
    Exception? throwOnAcquire = null,
    Exception? throwOnRelease = null) : IPostgreSqlAdvisoryLock
{
    public string? AcquiredKey { get; private set; }
    public string? ReleasedKey { get; private set; }
    public TimeSpan? AcquiredTimeout { get; private set; }

    public CancellationToken? ReleasedCancellationToken { get; private set; }

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
        ReleasedCancellationToken = cancellationToken;
        if (throwOnRelease is not null) throw throwOnRelease;
        return Task.FromResult(releaseResult);
    }
}
