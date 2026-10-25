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
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

/// <summary>
/// When AcquireAsync throws, BeginAsync propagates the failure unwrapped and DisposeAsync neither throws nor releases the
/// never-acquired lock (ADR 0058 §B.3). <c>await using</c> would substitute an exception thrown by DisposeAsync, so asserting
/// the original type pins both halves.
/// </summary>
public class PostgreSqlProvisioningUnitOfWorkBeginThrowsTests : IAsyncLifetime
{
    private readonly NpgsqlConnection _connection = new(PostgreSqlSettings.TestsBrighterConnectionString);
    private readonly FakePostgreSqlAdvisoryLock _advisoryLock = new(
        releaseResult: true,
        throwOnAcquire: new TimeoutException("forced lock-acquisition failure for spec 0028 §B.3 test"));

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task When_postgres_provisioning_uow_begin_throws_it_should_dispose_without_throwing()
    {
        // Act
        var thrown = await Record.ExceptionAsync(async () =>
        {
            await using var uow = new PostgreSqlProvisioningUnitOfWork(
                _connection, _advisoryLock, NullLogger.Instance);
            await uow.BeginAsync(
                lockResource: "test_lock_resource",
                lockTimeout: TimeSpan.FromSeconds(5),
                cancellationToken: CancellationToken.None);
        });

        // Assert
        Assert.IsType<TimeoutException>(thrown);
        Assert.Null(_advisoryLock.ReleasedKey);
    }
}
