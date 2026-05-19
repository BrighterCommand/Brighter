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
using Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.BoxProvisioning.Tests;

/// <summary>
/// <see cref="SqlBoxProvisioner{TConnection, TTransaction}.ProvisionAsync"/> is the framework
/// chokepoint for the provisioner. It reads <c>BoxTableName</c> from configuration and passes
/// it through to the detection helper, payload validator, and runner. Catalogs guard their own
/// <c>All</c> entry, but the provisioner opens connections and runs detection BEFORE the catalog
/// is invoked, so an unsafe identifier reaches the live DB connection unless the provisioner
/// guards at its own entry. This test pins defence-in-depth: an unsafe
/// <see cref="IAmARelationalDatabaseConfiguration.OutBoxTableName"/> must fail with
/// <see cref="ConfigurationException"/> BEFORE the first <c>CreateConnection</c>.
/// </summary>
public class SqlBoxProvisionerIdentifierValidationTests
{
    [Fact]
    public async Task When_outbox_table_name_is_unsafe_it_should_throw_before_creating_a_connection()
    {
        //Arrange
        var provisioner = new IdentifierProbeProvisioner(
            new IdentifierProbeConfiguration(outBoxTableName: "Outbox; DROP TABLE x", schemaName: "dbo"));

        //Act
        var thrown = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert
        Assert.IsType<ConfigurationException>(thrown);
        Assert.False(provisioner.CreateConnectionCalled);
    }

    [Fact]
    public async Task When_schema_name_is_unsafe_it_should_throw_before_creating_a_connection()
    {
        //Arrange
        var provisioner = new IdentifierProbeProvisioner(
            new IdentifierProbeConfiguration(outBoxTableName: "Outbox", schemaName: "dbo; --"));

        //Act
        var thrown = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert
        Assert.IsType<ConfigurationException>(thrown);
        Assert.False(provisioner.CreateConnectionCalled);
    }

    [Fact]
    public async Task When_schema_name_is_null_it_should_not_throw_for_identifier_validation()
    {
        //Arrange: SQLite has no schema concept — null SchemaName must pass identifier
        //         validation. Pinning this prevents an over-eager AssertSafe(null) from
        //         regressing the SQLite provisioner path.
        var provisioner = new IdentifierProbeProvisioner(
            new IdentifierProbeConfiguration(outBoxTableName: "Outbox", schemaName: null));

        //Act
        var thrown = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert: not a ConfigurationException — provisioner gets past the identifier guard
        //        and reaches CreateConnection, which throws NotSupportedException.
        Assert.IsNotType<ConfigurationException>(thrown);
        Assert.True(provisioner.CreateConnectionCalled);
    }

    /// <summary>
    /// Records whether <c>CreateConnection</c> was invoked. The identifier guard must fire
    /// BEFORE the first connection is created — if validation regresses to AFTER
    /// CreateConnection, either <see cref="CreateConnectionCalled"/> flips true or the
    /// resulting NotSupportedException replaces the expected ConfigurationException.
    /// </summary>
    private sealed class IdentifierProbeProvisioner : SqlBoxProvisioner<FakeDbConnection, FakeDbTransaction>
    {
        public bool CreateConnectionCalled { get; private set; }

        public IdentifierProbeProvisioner(IAmARelationalDatabaseConfiguration configuration)
            : base(
                new StubBoxDetectionHelper(),
                new EmptyCatalog(),
                new ThrowingPayloadValidator(),
                configuration,
                new ThrowingMigrationRunner(),
                BoxType.Outbox)
        {
        }

        protected override FakeDbConnection CreateConnection(string connectionString)
        {
            CreateConnectionCalled = true;
            throw new NotSupportedException("CreateConnection must not be reached when identifier validation throws.");
        }

        protected override string PayloadColumnName => "Body";
    }

    private sealed class IdentifierProbeConfiguration : IAmARelationalDatabaseConfiguration
    {
        public IdentifierProbeConfiguration(string outBoxTableName, string? schemaName)
        {
            OutBoxTableName = outBoxTableName;
            SchemaName = schemaName;
        }

        public bool BinaryMessagePayload => false;
        public bool JsonMessagePayload => false;
        public string ConnectionString => "Server=fake;Database=fake;";
        public string DatabaseName => "fake";
        public string InBoxTableName => "Commands";
        public string OutBoxTableName { get; }
        public string QueueStoreTable => "Queues";
        public string? SchemaName { get; }
    }

    private sealed class EmptyCatalog : IAmABoxMigrationCatalog
    {
        public IReadOnlyList<IAmABoxMigration> All(IAmARelationalDatabaseConfiguration configuration)
            => Array.Empty<IAmABoxMigration>();

        public string FreshInstallDdl(IAmARelationalDatabaseConfiguration configuration)
            => string.Empty;
    }

    private sealed class ThrowingPayloadValidator : IAmABoxPayloadModeValidator<FakeDbConnection>
    {
        public Task ValidateAsync(
            FakeDbConnection connection,
            string tableName,
            string? schemaName,
            string columnName,
            bool binaryMessagePayload,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("ValidateAsync must not be reached when identifier validation throws.");
    }

    private sealed class ThrowingMigrationRunner : IAmABoxMigrationRunner
    {
        public Task MigrateAsync(
            string tableName,
            string? schemaName,
            BoxType boxType,
            BoxTableState tableState,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("MigrateAsync must not be reached when identifier validation throws.");
    }
}
