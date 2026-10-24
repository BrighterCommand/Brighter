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
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

/// <summary>
/// <see cref="MsSqlAdvisoryLock"/> rejects a lock timeout that does not fit <c>sp_getapplock</c>'s
/// <c>INT</c> milliseconds argument, and accepts <c>int.MaxValue</c> milliseconds (ADR 0057 §5b).
/// </summary>
public class MsSqlAdvisoryLockTimeoutValidationTests
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;

    [Fact]
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

    [Fact]
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

    [Fact]
    public async Task When_lock_timeout_is_at_int_max_milliseconds_acquire_should_succeed()
    {
        //Arrange
        Configuration.EnsureDatabaseExists(_connectionString);
        var boundaryTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
        var lockResource = $"BrighterMigration_acquire_boundary_{Guid.NewGuid():N}";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        var advisoryLock = new MsSqlAdvisoryLock();

        //Act
        var ex = await Record.ExceptionAsync(() =>
            advisoryLock.AcquireAsync(
                connection, transaction, lockResource, boundaryTimeout, default));

        //Assert
        Assert.IsNotType<ArgumentOutOfRangeException>(ex);
        Assert.Null(ex);

        await transaction.RollbackAsync();
    }
}
