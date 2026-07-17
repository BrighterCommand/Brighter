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
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning.TestDoubles;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// Companion to <c>PostgreSqlProvisioningUnitOfWorkBeginTransactionThrowsTests</c>:
/// that test pins the basic cleanup-release contract; THIS test pins that the cleanup
/// release uses <see cref="CancellationToken.None"/>, not the caller-supplied token.
/// <para>
/// Why this matters: the production <c>PostgreSqlAdvisoryLock.ReleaseAsync</c> runs
/// <c>SELECT pg_advisory_unlock(...)</c> via <c>ExecuteScalarAsync(cancellationToken)</c>.
/// If the caller's token is already cancelled at the moment <c>BeginAsync</c>'s catch path
/// fires, passing that same token to <c>ReleaseAsync</c> short-circuits the unlock with
/// <see cref="OperationCanceledException"/> before the SQL is dispatched. The outer
/// try/catch swallows-and-logs that exception (rollback-must-not-throw semantics), so the
/// session-scoped lock leaks until connection close. Cleanup MUST use
/// <see cref="CancellationToken.None"/> — matching the §B.3 rollback contract on
/// <c>RollbackAsync</c> — to guarantee the unlock attempt actually reaches the server.
/// </para>
/// </summary>
/// <remarks>
/// Same forcing mechanism as the sibling test: opening an outer transaction makes any
/// subsequent <c>BeginTransactionAsync</c> on the same connection throw
/// <see cref="InvalidOperationException"/> ("nested/concurrent transactions aren't
/// supported"). That InvalidOperationException is thrown unconditionally by Npgsql's
/// nested-tx guard BEFORE the framework's cancellation check, so we observe it even with
/// a pre-cancelled token — letting us reach the cleanup release path while the token is
/// in a cancelled state. The fake lock then captures the token actually handed to
/// <c>ReleaseAsync</c> for direct assertion.
/// </remarks>
public class PostgreSqlProvisioningUnitOfWorkBeginTransactionCancelledTests
{
    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock = new(releaseResult: true);
    private NpgsqlTransaction? _blockingTransaction;

    [Before(Test)]
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _blockingTransaction = await _connection.BeginTransactionAsync();
    }

    [After(Test)]
    public async Task DisposeAsync()
    {
        if (_blockingTransaction is not null) await _blockingTransaction.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task When_postgres_provisioning_uow_begin_transaction_throws_with_cancelled_token_it_should_still_release_the_lock()
    {
        const string lockKey = "test_lock_resource_cancelled_token_cleanup";

        await using var uow = new PostgreSqlProvisioningUnitOfWork(
            _connection, _advisoryLock, NullLogger.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelledToken = cts.Token;

        var thrown = await TestExceptionRecorder.CaptureAsync(() => uow.BeginAsync(
            lockResource: lockKey,
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: cancelledToken));

        // Npgsql's nested-tx guard fires before the cancellation check, so the propagated
        // exception is the unwrapped InvalidOperationException from BeginTransactionAsync.
        await Assert.That(thrown).IsTypeOf<InvalidOperationException>();

        // The cleanup release ran (basic contract — guarded by the sibling test).
        await Assert.That(_advisoryLock.ReleasedKey).IsEqualTo(lockKey);

        // The cleanup release was handed a token that is NOT cancelled. Without the fix
        // (pass CancellationToken.None instead of the caller's token), this captured token
        // would equal the cancelled cancelledToken and IsCancellationRequested would be true,
        // meaning the real PostgreSqlAdvisoryLock.ReleaseAsync would have thrown
        // OperationCanceledException before issuing pg_advisory_unlock and the lock would
        // leak to connection close.
        await Assert.That(_advisoryLock.ReleasedCancellationToken).IsNotNull();
        await Assert.That(_advisoryLock.ReleasedCancellationToken!.Value.IsCancellationRequested).IsFalse();
    }
}
