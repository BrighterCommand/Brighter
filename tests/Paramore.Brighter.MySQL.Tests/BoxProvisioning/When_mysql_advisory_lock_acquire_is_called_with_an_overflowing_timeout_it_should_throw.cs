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
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// <see cref="MySqlAdvisoryLock"/> rejects a lock timeout that is negative or does not fit
/// <c>GET_LOCK</c>'s <c>INT</c> seconds argument, and accepts <c>int.MaxValue</c> seconds (ADR 0057 §5b).
/// </summary>
public class MySqlAdvisoryLockTimeoutValidationTests
{
    private readonly string _connectionString = Const.DefaultConnectingString;

    [Fact]
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

    [Fact]
    public async Task When_lock_timeout_exceeds_int_max_seconds_acquire_should_throw()
    {
        //Arrange
        var overflowingTimeout = TimeSpan.FromSeconds((double)int.MaxValue + 1);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var advisoryLock = new MySqlAdvisoryLock();

        //Act + Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            advisoryLock.AcquireAsync(
                connection, "any_resource", overflowingTimeout, default));
    }

    [Fact]
    public async Task When_lock_timeout_is_at_int_max_seconds_acquire_should_succeed()
    {
        //Arrange
        var boundaryTimeout = TimeSpan.FromSeconds(int.MaxValue);
        var lockResource = $"bm_bnd_{Guid.NewGuid():N}";

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var advisoryLock = new MySqlAdvisoryLock();

        //Act
        var ex = await Record.ExceptionAsync(() =>
            advisoryLock.AcquireAsync(
                connection, lockResource, boundaryTimeout, default));

        //Assert
        Assert.IsNotType<ArgumentOutOfRangeException>(ex);
        Assert.Null(ex);
    }
}
