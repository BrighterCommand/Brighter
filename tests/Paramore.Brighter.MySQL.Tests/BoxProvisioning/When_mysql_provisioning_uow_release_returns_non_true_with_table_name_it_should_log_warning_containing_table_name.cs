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
/// When the UoW is given a table name, its Warning for a 0 or NULL <c>RELEASE_LOCK</c> names the table
/// as well as the lock key, since a long key is hashed and no longer contains it (ADR 0057 §5b).
/// </summary>
public class MySqlProvisioningUnitOfWorkReleaseNonTrueTests : IAsyncLifetime
{
    private readonly MySqlConnection _connection = new(Const.DefaultConnectingString);

    public async Task InitializeAsync() => await _connection.OpenAsync();
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Theory]
    [InlineData(false, "0")]
    [InlineData(null, "NULL")]
    public async Task When_mysql_provisioning_uow_release_returns_non_true_with_table_name_it_should_log_warning_containing_table_name(
        bool? releaseResult, string expectedMarker)
    {
        // Arrange
        const string tableName = "test_outbox_3f9d2a1b8c7e4f0a9d6b5c3a1f8e7d2b";
        const string lockResource = "BrighterMigration_BrighterTests.test_outbox_3f9_a1b2c3d4e5f60718";

        var advisoryLock = new FakeMySqlAdvisoryLock(releaseResult);
        var logger = new CapturingLogger();

        await using var uow = new MySqlProvisioningUnitOfWork(_connection, advisoryLock, logger, tableName);
        await uow.BeginAsync(lockResource, TimeSpan.FromSeconds(5), CancellationToken.None);

        // Act
        var thrown = await Record.ExceptionAsync(() => uow.CommitAsync(CancellationToken.None));

        // Assert
        Assert.Null(thrown);
        Assert.Equal(lockResource, advisoryLock.ReleasedKey);

        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Single(warnings);
        Assert.Contains(tableName, warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains(lockResource, warnings[0].Message, StringComparison.Ordinal);
        Assert.Contains(expectedMarker, warnings[0].Message, StringComparison.Ordinal);
        Assert.Null(warnings[0].Exception);
    }
}
