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
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

public class MySqlAdvisoryLockTimeoutValidationTests
{
    // GET_LOCK takes its @timeout via a SQL INT bind (whole seconds), so any TimeSpan whose
    // ceil(TotalSeconds) exceeds int.MaxValue silently overflows on cast and may wrap to a
    // negative int — which GET_LOCK then interprets as "wait forever". A negative TimeSpan has
    // no meaningful interpretation for an exclusive application lock and would otherwise be
    // silently floored to a 1-second wait by the `Math.Max(1, Math.Ceiling(...))` shape,
    // masking the bad input. Per ADR 0057 §5b the validation lives inside the abstraction's
    // acquire path so any caller of MySqlAdvisoryLock — including but not limited to
    // MySqlBoxMigrationRunner — is protected. The boundary value (int.MaxValue seconds ≈ 68
    // years) is the largest value that fits GET_LOCK's INT @timeout argument without overflow
    // and must be accepted by the validation guard. Mirrors
    // `MsSqlAdvisoryLockTimeoutValidationTests` per PR #4039 review item #7; the MSSQL boundary
    // is int.MaxValue *milliseconds* (~24.85 days) because sp_getapplock takes ms.
    private readonly string _connectionString = Const.DefaultConnectingString;

    [Test]
    public async Task When_lock_timeout_is_negative_acquire_should_throw()
    {
        //Arrange
        var negativeTimeout = TimeSpan.FromMilliseconds(-1);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var advisoryLock = new MySqlAdvisoryLock();

        //Act + Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            advisoryLock.AcquireAsync(
                connection, "any_resource", negativeTimeout, default));
    }

    [Test]
    public async Task When_lock_timeout_exceeds_int_max_seconds_acquire_should_throw()
    {
        //Arrange — GET_LOCK takes its timeout via an INT bind (whole seconds), so any TimeSpan
        //whose ceil(TotalSeconds) exceeds int.MaxValue silently overflows on cast (wraps to a
        //negative int) and GET_LOCK then interprets the bind as "wait forever" — defeating the
        //migration lock for callers expecting bounded waits. Surface as ArgumentOutOfRangeException
        //so the failure mode at acquire time is a clear diagnostic rather than a deadlocked
        //deployment. Mirrors `MsSqlAdvisoryLockTimeoutValidationTests` per PR #4039 review item #7;
        //note the MSSQL boundary is int.MaxValue *milliseconds* (~24.85 days) because sp_getapplock
        //takes ms, whereas GET_LOCK takes seconds — so the MySQL boundary is int.MaxValue seconds
        //(~68 years).
        var overflowingTimeout = TimeSpan.FromSeconds((double)int.MaxValue + 1);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var advisoryLock = new MySqlAdvisoryLock();

        //Act + Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            advisoryLock.AcquireAsync(
                connection, "any_resource", overflowingTimeout, default));
    }

    [Test]
    public async Task When_lock_timeout_is_at_int_max_seconds_acquire_should_succeed()
    {
        //Arrange — the inclusive upper bound (int.MaxValue seconds ≈ 68 years) is the largest
        //value that fits GET_LOCK's INT @timeout argument without overflow; it must NOT be
        //rejected by the validation guard. Acquisition itself succeeds because no other session
        //holds the lock — GET_LOCK returns 1 immediately. Regression pin against any future
        //tightening of the overflow guard from `>` to `>=`, which would silently reject a
        //legitimate boundary input. Unique lock name avoids contention with parallel test runs;
        //short prefix because MySQL caps user-level lock names at 64 chars (32-char Guid + a
        //"bm_bnd_" prefix fits comfortably; MSSQL's 255-char @Resource limit does not apply).
        var boundaryTimeout = TimeSpan.FromSeconds(int.MaxValue);
        var lockResource = $"bm_bnd_{Guid.NewGuid():N}";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var advisoryLock = new MySqlAdvisoryLock();

        //Act
        var ex = await TestExceptionRecorder.CaptureAsync(() =>
            advisoryLock.AcquireAsync(
                connection, lockResource, boundaryTimeout, default));

        //Assert — boundary must not be rejected by the validation guard.
        await Assert.That(ex).IsNull();

        //Cleanup — session-scoped GET_LOCK is released by the `await using` connection dispose.
    }
}
