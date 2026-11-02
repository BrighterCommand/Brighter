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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.BoxProvisioning;

/// <summary>
/// The MySQL provisioners' pre-lock detection clamps a negative version (the -1 "discriminator
/// missing" sentinel) to 0 in the <see cref="BoxTableState"/> handed to the runner, as the other
/// relational backends do.
/// </summary>
/// <remarks>
/// The stub detection helper reports a table without history, the only branch where the clamp applies;
/// it throws <see cref="NotSupportedException"/> from <c>GetMaxVersionAsync</c> so taking the other branch
/// fails loudly. The connection must still open, so MySQL must be running.
/// </remarks>
public class MySqlPreLockNegativeVersionClampTests
{
    private const string ConnectionString = Const.DefaultConnectingString;

    [Fact]
    public async Task When_mysql_outbox_provisioner_pre_lock_detection_returns_negative_version_it_should_clamp_to_zero()
    {
        //Arrange
        var detection = new BootstrapDetectionHelper { DetectedVersionResult = -1 };
        var migrationRunner = new VersionCapturingMigrationRunner();
        var config = new RelationalDatabaseConfiguration(
            ConnectionString,
            outBoxTableName: $"test_outbox_{Guid.NewGuid():N}");
        var provisioner = new MySqlOutboxProvisioner(
            detection,
            new MySqlOutboxMigrationCatalog(),
            new NoOpPayloadValidator(),
            config,
            migrationRunner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.Equal(0, migrationRunner.CapturedTableState!.CurrentVersion);
    }

    [Fact]
    public async Task When_mysql_inbox_provisioner_pre_lock_detection_returns_negative_version_it_should_clamp_to_zero()
    {
        //Arrange
        var detection = new BootstrapDetectionHelper { DetectedVersionResult = -1 };
        var migrationRunner = new VersionCapturingMigrationRunner();
        var config = new RelationalDatabaseConfiguration(
            ConnectionString,
            inboxTableName: $"test_inbox_{Guid.NewGuid():N}");
        var provisioner = new MySqlInboxProvisioner(
            detection,
            new MySqlInboxMigrationCatalog(),
            new NoOpPayloadValidator(),
            config,
            migrationRunner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert
        Assert.NotNull(migrationRunner.CapturedTableState);
        Assert.Equal(0, migrationRunner.CapturedTableState!.CurrentVersion);
    }

    private sealed class BootstrapDetectionHelper : IAmAVersionDetectingMigrationHelper<MySqlConnection, MySqlTransaction>
    {
        public int DetectedVersionResult { get; set; }

        public Task<bool> DoesTableExistAsync(
            MySqlConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            MySqlTransaction? transaction = null)
            => Task.FromResult(true);

        public Task<bool> DoesHistoryExistAsync(
            MySqlConnection connection, string tableName, string? schemaName, string? historySchema,
            CancellationToken cancellationToken = default,
            MySqlTransaction? transaction = null)
            => Task.FromResult(false);

        public Task<int> GetMaxVersionAsync(
            MySqlConnection connection, string tableName, string? schemaName, string? historySchema,
            CancellationToken cancellationToken = default,
            MySqlTransaction? transaction = null)
            => throw new NotSupportedException("Bootstrap branch should not call GetMaxVersionAsync.");

        public Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
            MySqlConnection connection, string tableName, string? schemaName,
            CancellationToken cancellationToken = default,
            MySqlTransaction? transaction = null)
            => throw new NotSupportedException("Stub helper does not service column probes.");

        public string DiscriminatorFor(BoxType boxType)
            => throw new NotSupportedException("Stub helper does not service discriminator probes.");

        public Task<int> DetectCurrentVersionAsync(
            MySqlConnection connection, string tableName, string? schemaName,
            BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
            CancellationToken cancellationToken = default,
            MySqlTransaction? transaction = null)
            => Task.FromResult(DetectedVersionResult);
    }

    private sealed class NoOpPayloadValidator : IAmABoxPayloadModeValidator<MySqlConnection>
    {
        public Task ValidateAsync(
            MySqlConnection connection,
            string tableName,
            string? schemaName,
            string columnName,
            bool binaryMessagePayload,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class VersionCapturingMigrationRunner : IAmABoxMigrationRunner
    {
        public BoxTableState? CapturedTableState { get; private set; }

        public Task MigrateAsync(
            BoxTableName tableName,
            SchemaName? schemaName,
            BoxType boxType,
            BoxTableState tableState,
            CancellationToken cancellationToken = default)
        {
            CapturedTableState = tableState;
            return Task.CompletedTask;
        }
    }
}
