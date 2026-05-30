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

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

// Spec 0029 NF3/AC6 (ADR 0060 D2/D4): under PerSchema, a SchemaName containing an
// injection-style token must be rejected by Identifiers.AssertSafe before any DDL with
// the raw identifier is emitted. The existing
// When_mssql_outbox_migrations_are_built_with_an_unsafe_schema_name_it_should_throw analog
// pins the catalog-factory entry (independent of scope); this test pins the runner-driven
// PerSchema path end-to-end via ProvisionAsync, so a future contributor cannot bypass the
// catalog and reach a per-schema DDL site without tripping a validation gate.
public class MsSqlPerSchemaUnsafeSchemaNameTests : IAsyncLifetime
{
    // Evident data: a SchemaName that, if interpolated raw into `CREATE SCHEMA [{schema}]`,
    // would close the bracket-quoted identifier and append further statements. Any of `]`,
    // `;`, `-`, or space alone is enough to fail the ^[A-Za-z][A-Za-z0-9_]*$ regex — the
    // combination is the realistic injection payload an operator might cargo-cult from a
    // misconfigured tenant ID.
    private const string UnsafeSchemaName = "bad];drop table foo;--";
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _boxTableName = $"test_outbox_{Guid.NewGuid():N}";

    [Fact]
    public async Task When_per_schema_scope_is_selected_with_an_unsafe_schema_name_mssql_runner_should_throw()
    {
        //Arrange — MigrationHistoryScope.PerSchema + the unsafe SchemaName. The operator-facing
        //entrypoint is the provisioner, so we drive ProvisionAsync — this is the path that would
        //emit DDL on a safe input.
        Configuration.EnsureDatabaseExists(_connectionString);
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _boxTableName,
            schemaName: UnsafeSchemaName);
        var runner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
        var provisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);

        //Act
        var thrown = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert — ConfigurationException is the documented contract (Identifiers.AssertSafe);
        //the message must echo the unsafe value so operators can see which configured identifier
        //was rejected; and no DDL with the raw literal was emitted — assert by absence of a
        //matching schema in sys.schemas (which would only exist if a CREATE SCHEMA had escaped
        //the validation gate).
        var configEx = Assert.IsType<ConfigurationException>(thrown);
        Assert.Contains(UnsafeSchemaName, configEx.Message);
        Assert.False(SchemaExists(UnsafeSchemaName),
            $"sys.schemas should not contain '{UnsafeSchemaName}' — validation must run before DDL.");
    }

    private bool SchemaExists(string schemaName)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sys.schemas WHERE name = @SchemaName;";
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        return (int)command.ExecuteScalar()! > 0;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        // No teardown needed: the validation gate throws before any DDL runs, so there is
        // nothing to drop. The catch is defensive against a future regression where validation
        // is moved further down the path.
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP SCHEMA IF EXISTS [bad];";
            command.ExecuteNonQuery();
        }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
