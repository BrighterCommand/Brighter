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

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// When <c>RELEASE_LOCK</c> throws during <c>RollbackAsync</c>, the UoW logs a Warning naming the lock
/// and carrying the exception, and does not rethrow: a throw would hide the migration's own failure in
/// the runner's catch (ADR 0058 §B.3).
/// </summary>
public class MySqlProvisioningUnitOfWorkRollbackReleaseThrowsTests
{
    private readonly MySqlConnection _connection = new(Const.DefaultConnectingString);

    [Before(HookType.Test)]

    public async Task InitializeAsync() => await _connection.OpenAsync();

    [After(HookType.Test)]

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Test]
    public async Task When_mysql_provisioning_uow_rollback_release_throws_it_should_log_without_throwing()
    {
        // Arrange
        var releaseFault = new InvalidOperationException("Connection is broken");
        var advisoryLock = new FakeMySqlAdvisoryLock(
            releaseResult: true,
            throwOnRelease: releaseFault);
        var logger = new CapturingLogger();

        await using var uow = new MySqlProvisioningUnitOfWork(_connection, advisoryLock, logger);
        await uow.BeginAsync(
            lockResource: "test_lock_resource",
            lockTimeout: TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        // Act
        var thrown = await TestExceptionRecorder.CaptureAsync(() => uow.RollbackAsync(CancellationToken.None));

        // Assert
        await Assert.That(thrown).IsNull();
        await Assert.That(advisoryLock.ReleasedKey).IsEqualTo("test_lock_resource");

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        await Assert.That(warnings).HasSingleItem();
        await Assert.That(warnings[0].Message).Contains("test_lock_resource");
        await Assert.That(warnings[0].Exception).IsSameReferenceAs(releaseFault);
    }
}
