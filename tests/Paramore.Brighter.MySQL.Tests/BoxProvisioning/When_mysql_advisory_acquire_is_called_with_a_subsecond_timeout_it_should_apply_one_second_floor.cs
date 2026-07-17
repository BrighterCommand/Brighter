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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

// Item R (spec 0027 PR #4039 third review). Pins the sub-second-timeout-floor correctness
// contract on MySqlAdvisoryLock.AcquireAsync: `(int)timeout.TotalSeconds` truncates any
// TimeSpan with TotalSeconds < 1 to 0, which GET_LOCK(name, 0) interprets as non-blocking
// (return immediately). A caller setting MigrationLockTimeout = TimeSpan.FromMilliseconds(500)
// would get an effectively-no-wait acquire that fails on any contention. The floor
// Math.Max(1, Math.Ceiling(timeout.TotalSeconds)) guarantees at least 1s of server-side
// blocking even for sub-second TimeSpan inputs.
//
// The companion NULL-vs-timeout disambiguation contract (GET_LOCK NULL → new
// MySqlAdvisoryLockException, 0 → keep TimeoutException) is applied in production code as
// defensive coverage but cannot be exercised by an integration test against MySQL 8 — that
// version rejects NULL/invalid lock names with a typed connector-level exception before
// GET_LOCK ever returns NULL, and the only remaining reachable NULL paths (server OOM,
// KILL CONNECTION mid-call) are racy to drive deterministically. ADR 0057 §5b lists the
// per-code mapping for completeness.

public class MySqlAdvisoryAcquireSubsecondTimeoutFloorTests
{
    private readonly string _connectionString = Const.DefaultConnectingString;

    [Test]
    public async Task When_acquire_is_called_with_a_subsecond_timeout_it_should_block_at_least_one_second()
    {
        //Arrange — a holder session pre-acquires the lock; the system-under-test session then
        //attempts to acquire the same lock with a 500ms timeout. With the bug, (int)0.5 → 0
        //and GET_LOCK returns immediately as non-blocking; with the floor applied, GET_LOCK
        //blocks server-side for the floored 1s before timing out.
        var lockKey = $"R_floor_block_{Guid.NewGuid():N}";
        var advisoryLock = new MySqlAdvisoryLock();

        await using var holder = await OpenAsync();
        await advisoryLock.AcquireAsync(holder, lockKey, TimeSpan.FromSeconds(30), CancellationToken.None);
        try
        {
            await using var contender = await OpenAsync();
            var stopwatch = Stopwatch.StartNew();

            //Act + Assert — a 500ms TimeSpan must still block server-side for ≥ ~1s.
            await Assert.ThrowsAsync<TimeoutException>(() =>
                advisoryLock.AcquireAsync(contender, lockKey, TimeSpan.FromMilliseconds(500), CancellationToken.None));

            stopwatch.Stop();
            await Assert.That(stopwatch.ElapsedMilliseconds >= 900).IsTrue().Because($"Expected acquire to block for ≥ 900ms (one-second floor minus jitter); blocked for {stopwatch.ElapsedMilliseconds}ms.");
        }
        finally
        {
            await advisoryLock.ReleaseAsync(holder, lockKey, CancellationToken.None);
        }
    }

    [Test]
    public async Task When_acquire_blocks_until_lock_available_within_subsecond_timeout_it_should_succeed_after_floor_applied()
    {
        //Arrange — holder pre-acquires the lock; we schedule a release at ~300ms; the
        //contender then attempts to acquire with a 500ms TimeSpan. With the bug, the
        //contender returns immediately as TimeoutException (truncated to 0). With the floor,
        //the contender blocks for up to 1s, sees the holder's release at 300ms, and succeeds.
        var lockKey = $"R_floor_succeed_{Guid.NewGuid():N}";
        var advisoryLock = new MySqlAdvisoryLock();

        await using var holder = await OpenAsync();
        await advisoryLock.AcquireAsync(holder, lockKey, TimeSpan.FromSeconds(30), CancellationToken.None);

        var releaseAfter = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            await advisoryLock.ReleaseAsync(holder, lockKey, CancellationToken.None);
        });

        await using var contender = await OpenAsync();
        try
        {
            //Act — must succeed (no exception) because the floor extends the wait to 1s.
            await advisoryLock.AcquireAsync(contender, lockKey, TimeSpan.FromMilliseconds(500), CancellationToken.None);

            //Assert — releasing on the contender after a successful acquire must return true.
            var releaseResult = await advisoryLock.ReleaseAsync(contender, lockKey, CancellationToken.None);
            await Assert.That(releaseResult).IsTrue();
        }
        finally
        {
            await releaseAfter;
        }
    }

    private async Task<MySqlConnection> OpenAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}