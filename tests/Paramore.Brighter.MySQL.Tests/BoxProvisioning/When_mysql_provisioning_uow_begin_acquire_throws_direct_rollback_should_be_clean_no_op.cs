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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.MySQL.Tests.BoxProvisioning.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// A <c>RollbackAsync</c> called directly after a failed <c>BeginAsync</c> is a no-op: it issues no
/// <c>RELEASE_LOCK</c> on the lock it never took, which would return NULL and log a misleading
/// Warning (ADR 0058 §B.3).
/// </summary>
public class MySqlProvisioningUnitOfWorkBeginAcquireThrowsTests : IAsyncLifetime
{
    private readonly MySqlConnection _connection = new(Const.DefaultConnectingString);
    private readonly FakeMySqlAdvisoryLock _advisoryLock = new(
        releaseResult: null,
        throwOnAcquire: new TimeoutException("forced lock-acquisition failure for F2 reviewer test"));

    public async Task InitializeAsync() => await _connection.OpenAsync();

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task When_mysql_provisioning_uow_begin_acquire_throws_direct_rollback_should_be_clean_no_op()
    {
        // Arrange
        var logger = new CapturingLogger();
        var uow = new MySqlProvisioningUnitOfWork(_connection, _advisoryLock, logger);

        // Act
        var beginThrown = await Record.ExceptionAsync(() => uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None));
        Assert.IsType<TimeoutException>(beginThrown);

        var rollbackThrown = await Record.ExceptionAsync(() =>
            uow.RollbackAsync(CancellationToken.None));

        // Assert
        Assert.Null(rollbackThrown);
        Assert.Null(_advisoryLock.ReleasedKey);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Empty(warnings);
    }
}
