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
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class PostgreSqlAdvisoryLockDeadlineTimeProviderTests : IAsyncLifetime
{
    // PostgreSqlAdvisoryLock previously read DateTime.UtcNow to compute the timeout deadline.
    // A wall-clock jump (NTP correction during a long lock wait, leap-second smear, container
    // clock skew on VM resume) could collapse the deadline (premature TimeoutException) or
    // extend it (lock wait runs past the configured timeout). The fix injects TimeProvider so
    // the deadline math uses a monotonic, controllable source. This test pins the contract:
    // advancing FakeTimeProvider by timeout + 1s while the lock is genuinely held by another
    // session must cause Acquire to throw TimeoutException — the wall clock barely moves,
    // proving the deadline check is driven by the injected provider, not DateTime.UtcNow.

    // Mirror the namespace constant in PostgreSqlAdvisoryLock so the holder session's lock
    // collides on the same (namespace, hashtext(key)) pair the production code uses.
    private const int LOCK_NAMESPACE = 74726;

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _lockKey = $"BrighterMigrationTimeProviderTest_{Guid.NewGuid():N}";

    private NpgsqlConnection? _holderConnection;
    private NpgsqlConnection? _contenderConnection;

    [Fact]
    public async Task When_postgres_advisory_lock_deadline_is_evaluated_against_the_injected_time_provider_it_should_be_independent_of_wall_clock()
    {
        // Arrange — ensure database exists, then open two distinct sessions. The first
        //           genuinely holds the Postgres advisory lock (so any subsequent
        //           pg_try_advisory_lock on the same key returns false); the second drives
        //           the system under test through a fake clock.
        new PostgresSqlTestHelper().SetupDatabase();

        _holderConnection = new NpgsqlConnection(_connectionString);
        await _holderConnection.OpenAsync();
        await using (var hold = _holderConnection.CreateCommand())
        {
            hold.CommandText = "SELECT pg_advisory_lock(@ns, hashtext(@key))";
            hold.Parameters.AddWithValue("@ns", LOCK_NAMESPACE);
            hold.Parameters.AddWithValue("@key", _lockKey);
            await hold.ExecuteNonQueryAsync();
        }

        _contenderConnection = new NpgsqlConnection(_connectionString);
        await _contenderConnection.OpenAsync();

        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var advisoryLock = new PostgreSqlAdvisoryLock(fakeTimeProvider);

        // 30s is far longer than any wall-clock budget the test will tolerate (5s ceiling
        // below). If the deadline math read the wall clock the test would either hang to 30s
        // or fail the wall-clock assertion.
        var timeout = TimeSpan.FromSeconds(30);

        // Act — fire the acquire on a background task so we can advance the fake clock after
        //       startTimestamp has been captured. The capture is a synchronous call before
        //       the first await, so 300ms is comfortably enough for it to land.
        var stopwatch = Stopwatch.StartNew();
        var acquireTask = Task.Run(() => advisoryLock.AcquireAsync(
            _contenderConnection, _lockKey, timeout, CancellationToken.None));

        await Task.Delay(TimeSpan.FromMilliseconds(300));

        // Push the fake clock past the deadline. The next elapsed-time check inside the
        // retry loop must see elapsed >= timeout and throw.
        fakeTimeProvider.Advance(timeout + TimeSpan.FromSeconds(1));

        var thrown = await Assert.ThrowsAsync<TimeoutException>(() => acquireTask);
        stopwatch.Stop();

        // Assert — message names the lock key, and the wall clock barely moved. The 5s ceiling
        //          is generous for a CI-loaded run; the real wall-clock budget is dominated by
        //          the 100-1000ms exponential-backoff Task.Delay between retries.
        Assert.Contains(_lockKey, thrown.Message, StringComparison.Ordinal);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Wall-clock elapsed {stopwatch.Elapsed.TotalSeconds:F2}s — deadline should have fired against the fake provider, not the wall clock.");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            if (_holderConnection is not null)
            {
                await using (var release = _holderConnection.CreateCommand())
                {
                    release.CommandText = "SELECT pg_advisory_unlock(@ns, hashtext(@key))";
                    release.Parameters.AddWithValue("@ns", LOCK_NAMESPACE);
                    release.Parameters.AddWithValue("@key", _lockKey);
                    await release.ExecuteScalarAsync();
                }
                await _holderConnection.DisposeAsync();
            }

            if (_contenderConnection is not null)
            {
                await _contenderConnection.DisposeAsync();
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
