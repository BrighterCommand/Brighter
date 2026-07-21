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
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

public class MsSqlAdvisoryLockTimeoutValidationTests
{
    // sp_getapplock takes @LockTimeout as a SQL Server INT (milliseconds), so any TimeSpan
    // exceeding ~24.85 days silently overflows on cast and may produce -1 — which sp_getapplock
    // interprets as "wait indefinitely". A negative TimeSpan has no meaningful interpretation
    // for an exclusive application lock and would also overflow into one of sp_getapplock's
    // reserved sentinel values. Per ADR 0057 §5b the validation lives inside the abstraction's
    // acquire path so any caller of MsSqlAdvisoryLock — including but not limited to
    // MsSqlBoxMigrationRunner — is protected. The boundary value (int.MaxValue ms ≈ 24.85 days)
    // is the largest value that fits sp_getapplock's INT @LockTimeout argument without
    // overflow and must be accepted by the validation guard.
    private readonly string _connectionString = Configuration.DefaultConnectingString;

    [Test]
    public async Task When_lock_timeout_exceeds_int_max_milliseconds_acquire_should_throw()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);
        var overflowingTimeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        var advisoryLock = new MsSqlAdvisoryLock();

        //Act + Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            advisoryLock.AcquireAsync(
                connection, transaction, "any_resource", overflowingTimeout, default));
    }

    [Test]
    public async Task When_lock_timeout_is_negative_acquire_should_throw()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);
        var negativeTimeout = TimeSpan.FromMilliseconds(-1);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        var advisoryLock = new MsSqlAdvisoryLock();

        //Act + Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            advisoryLock.AcquireAsync(
                connection, transaction, "any_resource", negativeTimeout, default));
    }

    [Test]
    public async Task When_lock_timeout_is_at_int_max_milliseconds_acquire_should_succeed()
    {
        //Arrange — the inclusive upper bound (~24.85 days) is the largest value that fits
        //sp_getapplock's INT @LockTimeout argument without overflow; it must NOT be rejected
        //by the validation guard. Acquisition itself succeeds because no other session holds
        //the lock — sp_getapplock returns 0 (granted synchronously).
        Configuration.EnsureDatabaseExists(_connectionString);
        var boundaryTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
        var lockResource = $"BrighterMigration_acquire_boundary_{Guid.NewGuid():N}";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        var advisoryLock = new MsSqlAdvisoryLock();

        //Act
        var ex = await TestExceptionRecorder.CaptureAsync(() =>
            advisoryLock.AcquireAsync(
                connection, transaction, lockResource, boundaryTimeout, default));

        //Assert — boundary must not be rejected by the validation guard.
        await Assert.That(ex).IsNull();

        //Cleanup — release the transaction-scoped lock by rolling back.
        await transaction.RollbackAsync();
    }
}
