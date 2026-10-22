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
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// The advisory-lock deadline is measured with the injected <see cref="TimeProvider"/>, not the wall clock: while another
/// session holds the lock, advancing a <c>FakeTimeProvider</c> past the timeout makes AcquireAsync throw
/// <see cref="TimeoutException"/> long before the timeout passes in real time (ADR 0057 §5b).
/// </summary>
public class PostgreSqlAdvisoryLockDeadlineTimeProviderTests : IAsyncLifetime
{
    private const int LOCK_NAMESPACE = 74726;

    private static readonly TimeSpan s_lockTimeoutFarBeyondWallClockCeiling = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_wallClockCeiling = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan s_timeForAcquireToReadItsStartTimestamp = TimeSpan.FromMilliseconds(300);

    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _lockKey = $"BrighterMigrationTimeProviderTest_{Guid.NewGuid():N}";

    private NpgsqlConnection? _holderConnection;
    private NpgsqlConnection? _contenderConnection;

    [Fact]
    public async Task When_postgres_advisory_lock_deadline_is_evaluated_against_the_injected_time_provider_it_should_be_independent_of_wall_clock()
    {
        // Arrange
        new PostgresSqlTestHelper().SetupDatabase();

        _holderConnection = new NpgsqlConnection(_connectionString);
        await _holderConnection.OpenAsync();
        await using (var hold = _holderConnection.CreateCommand())
        {
            hold.CommandText = "SELECT pg_advisory_lock(@key)";
            hold.Parameters.AddWithValue("@key", DeriveLockKey(_lockKey));
            await hold.ExecuteNonQueryAsync();
        }

        _contenderConnection = new NpgsqlConnection(_connectionString);
        await _contenderConnection.OpenAsync();

        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var advisoryLock = new PostgreSqlAdvisoryLock(fakeTimeProvider);

        var timeout = s_lockTimeoutFarBeyondWallClockCeiling;

        // Act
        var stopwatch = Stopwatch.StartNew();
        var acquireTask = Task.Run(() => advisoryLock.AcquireAsync(
            _contenderConnection, _lockKey, timeout, CancellationToken.None));

        await Task.Delay(s_timeForAcquireToReadItsStartTimestamp);

        fakeTimeProvider.Advance(timeout + TimeSpan.FromSeconds(1));

        var thrown = await Assert.ThrowsAsync<TimeoutException>(() => acquireTask);
        stopwatch.Stop();

        // Assert
        Assert.Contains(_lockKey, thrown.Message, StringComparison.Ordinal);
        Assert.True(stopwatch.Elapsed < s_wallClockCeiling,
            $"Wall-clock elapsed {stopwatch.Elapsed.TotalSeconds:F2}s — deadline should have fired against the fake provider, not the wall clock.");
    }

    // Replicates the private PostgreSqlAdvisoryLock.DeriveLockKey (ADR 0062), so the holder session contends on the same key.
    private static long DeriveLockKey(string lockKey)
    {
        var composite = $"{LOCK_NAMESPACE}:{lockKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(composite));
        return BinaryPrimitives.ReadInt64BigEndian(hash);
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
                    release.CommandText = "SELECT pg_advisory_unlock(@key)";
                    release.Parameters.AddWithValue("@key", DeriveLockKey(_lockKey));
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
