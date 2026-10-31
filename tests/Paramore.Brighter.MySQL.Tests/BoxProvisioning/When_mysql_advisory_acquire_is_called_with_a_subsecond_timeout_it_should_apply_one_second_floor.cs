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
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// A sub-second timeout passed to <c>MySqlAdvisoryLock.AcquireAsync</c> is floored to one second, so
/// <c>GET_LOCK</c> still blocks server-side instead of truncating to 0 (non-blocking); see ADR 0057 §5b.
/// </summary>
public class MySqlAdvisoryAcquireSubsecondTimeoutFloorTests
{
    private readonly string _connectionString = Const.DefaultConnectingString;

    [Fact]
    public async Task When_acquire_is_called_with_a_subsecond_timeout_it_should_block_at_least_one_second()
    {
        //Arrange
        var lockKey = $"R_floor_block_{Guid.NewGuid():N}";
        var advisoryLock = new MySqlAdvisoryLock();

        await using var holder = await OpenAsync();
        await advisoryLock.AcquireAsync(holder, lockKey, TimeSpan.FromSeconds(30), CancellationToken.None);
        try
        {
            await using var contender = await OpenAsync();
            var stopwatch = Stopwatch.StartNew();

            //Act + Assert
            await Assert.ThrowsAsync<TimeoutException>(() =>
                advisoryLock.AcquireAsync(contender, lockKey, TimeSpan.FromMilliseconds(500), CancellationToken.None));

            stopwatch.Stop();
            Assert.True(
                stopwatch.ElapsedMilliseconds >= 900,
                $"Expected acquire to block for ≥ 900ms (one-second floor minus jitter); blocked for {stopwatch.ElapsedMilliseconds}ms.");
        }
        finally
        {
            await advisoryLock.ReleaseAsync(holder, lockKey, CancellationToken.None);
        }
    }

    [Fact]
    public async Task When_acquire_blocks_until_lock_available_within_subsecond_timeout_it_should_succeed_after_floor_applied()
    {
        //Arrange
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
            //Act
            await advisoryLock.AcquireAsync(contender, lockKey, TimeSpan.FromMilliseconds(500), CancellationToken.None);

            //Assert
            var releaseResult = await advisoryLock.ReleaseAsync(contender, lockKey, CancellationToken.None);
            Assert.True(releaseResult);
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
