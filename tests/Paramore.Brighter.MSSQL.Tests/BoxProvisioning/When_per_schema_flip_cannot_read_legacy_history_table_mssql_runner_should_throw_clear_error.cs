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
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

// Spec 0029 FR5 / AC5 (ADR 0060 D5 hardening, reviewer #1): the D5 seed copies legacy default-schema
// history rows into the new per-schema history on the first Global → PerSchema run. Multi-tenant
// deployments routinely run the provisioner under tenant-isolated credentials that have NO SELECT on
// [dbo]. Silently absorbing the failed seed would leave an empty per-schema history; the runner would
// then bootstrap-stamp a fresh row from the box-table columns on the next run, effectively re-applying
// the migration ledger and breaking FR5. The runner must therefore surface the failure as a
// ConfigurationException naming the cause, roll the surrounding transaction back, and leave NO empty
// per-schema stub behind that would defeat FR5 on retry.
//
// Test arrangement (least-privilege credential setup, documented in the helper):
//   - LOGIN + USER created with GRANT CREATE TABLE database-wide and GRANT CONTROL on the tenant
//     schema (so PerSchema CREATE/INSERT/SELECT against the tenant schema works).
//   - GRANT VIEW DEFINITION ON [dbo].[__BrighterMigrationHistory] so the table is visible in
//     sys.tables to the restricted principal (sys.tables filters to securables on which the user
//     has been granted SOME permission — a DENY alone does not satisfy that filter, so without an
//     accompanying GRANT the seed's existence probe would silently report "no legacy" and skip,
//     which is a separate gap not under test here).
//   - DENY SELECT ON [dbo].[__BrighterMigrationHistory] so the INSERT...SELECT inside the seed
//     fails with SqlException error 229 (permission denied), which the runner must convert to a
//     ConfigurationException with the documented message.
public class MsSqlPerSchemaFlipPermissionTests : IAsyncLifetime
{
    private readonly string _elevatedConnectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_perm_{Guid.NewGuid():N}";
    private readonly string _restrictedLogin = $"brighter_restricted_{Guid.NewGuid():N}".Substring(0, 32);
    private readonly string _restrictedPassword = $"Restricted_{Guid.NewGuid():N}!";
    private string? _restrictedConnectionString;

    [Fact]
    public async Task When_per_schema_flip_cannot_read_legacy_history_table_mssql_runner_should_throw_clear_error()
    {
        //Arrange — clean slate under elevated creds; operator pre-creates the tenant schema.
        Configuration.EnsureDatabaseExists(_elevatedConnectionString);
        EnsureSchemaExists(_schemaName);
        DropAnyExistingTable(_tableName, _schemaName);
        DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);
        DeleteDboHistoryRows();

        //Arrange — provision under Global with ELEVATED creds; populates [dbo].__BrighterMigrationHistory
        //with one row for this tenant. This is the legacy history the seed must copy on the flip.
        var globalProvisioner = BuildOutboxProvisioner(_elevatedConnectionString, MigrationHistoryScope.Global);
        await globalProvisioner.ProvisionAsync();
        Assert.Equal(1, GetHistoryRowCountInSchema("dbo"));

        //Arrange — create the restricted login/user with the minimum permissions documented at the top
        //of this file: enough to run a PerSchema provision against the tenant schema, but no SELECT
        //on [dbo].[__BrighterMigrationHistory]. The seed's INSERT...SELECT will fault and the runner
        //must convert that to a ConfigurationException.
        _restrictedConnectionString = CreateRestrictedLoginAndConnectionString();

        //Act — flip to PerSchema as the restricted principal.
        var perSchemaProvisioner = BuildOutboxProvisioner(_restrictedConnectionString, MigrationHistoryScope.PerSchema);
        var exception = await Record.ExceptionAsync(() => perSchemaProvisioner.ProvisionAsync());

        //Assert — the runner surfaces the failure as a ConfigurationException, NOT a raw SqlException.
        //The documented message phrase pins the user-facing cause (operators read this and grant SELECT
        //on the legacy table); the inner exception preserves the provider error for diagnostics.
        var configException = Assert.IsType<ConfigurationException>(exception);
        Assert.Contains(
            "the first Global → PerSchema run requires read access to the legacy default-schema history table",
            configException.Message);
        Assert.NotNull(configException.InnerException);

        //Assert — legacy [dbo].__BrighterMigrationHistory row is left intact: the seed is INSERT-only
        //into the per-schema target and the transaction roll-back undoes nothing on the legacy side.
        Assert.Equal(1, GetHistoryRowCountInSchema("dbo"));

        //Assert — per-schema [<tenant>].__BrighterMigrationHistory must NOT be left as an empty stub.
        //The CREATE TABLE and the failing seed both run inside the same provisioning transaction, so
        //the roll-back unwinds the CREATE. An empty per-schema history table left behind would defeat
        //FR5: the next provision would short-circuit the seed (table-present branch) and the runner's
        //bootstrap path would then stamp a fresh row from the box columns, silently re-applying the
        //migration ledger.
        Assert.False(
            TableExistsInSchema("__BrighterMigrationHistory", _schemaName),
            $"Per-schema history table must not be left as an empty stub in '{_schemaName}' after a failed flip.");
    }

    private MsSqlOutboxProvisioner BuildOutboxProvisioner(string connectionString, MigrationHistoryScope scope)
    {
        var config = new RelationalDatabaseConfiguration(
            connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var runner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: scope);
        return new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);
    }

    // Builds a least-privilege login/user (see file-header note for the rationale) and returns a
    // connection string bound to it. Runs under elevated creds against master (CREATE LOGIN) and
    // BrighterTests (CREATE USER + grants). Idempotent on cleanup via DisposeAsync.
    private string CreateRestrictedLoginAndConnectionString()
    {
        var elevatedBuilder = new SqlConnectionStringBuilder(_elevatedConnectionString);
        var databaseName = elevatedBuilder.InitialCatalog;

        // CREATE LOGIN must run in master; CHECK_POLICY=OFF avoids tripping the host's password
        // complexity policy on a randomly generated password.
        var masterBuilder = new SqlConnectionStringBuilder(_elevatedConnectionString) { InitialCatalog = "master" };
        ExecuteNonQuery(masterBuilder.ConnectionString,
            $"CREATE LOGIN [{_restrictedLogin}] WITH PASSWORD = N'{_restrictedPassword}', CHECK_POLICY = OFF;");

        // Map a DB user to the login, then grant the minimum set documented in the file header.
        ExecuteNonQuery(_elevatedConnectionString, $@"
CREATE USER [{_restrictedLogin}] FOR LOGIN [{_restrictedLogin}];
GRANT CREATE TABLE TO [{_restrictedLogin}];
GRANT CONTROL ON SCHEMA::[{_schemaName}] TO [{_restrictedLogin}];
GRANT VIEW DEFINITION ON [dbo].[__BrighterMigrationHistory] TO [{_restrictedLogin}];
DENY SELECT ON [dbo].[__BrighterMigrationHistory] TO [{_restrictedLogin}];
");

        var restrictedBuilder = new SqlConnectionStringBuilder(_elevatedConnectionString)
        {
            UserID = _restrictedLogin,
            Password = _restrictedPassword,
            IntegratedSecurity = false,
        };
        return restrictedBuilder.ConnectionString;
    }

    private void DropRestrictedLogin()
    {
        // Drop the DB user FIRST — DROP LOGIN refuses while a mapped user still references it.
        try
        {
            ExecuteNonQuery(_elevatedConnectionString, $"DROP USER IF EXISTS [{_restrictedLogin}];");
        }
        catch { /* best-effort cleanup */ }

        var masterBuilder = new SqlConnectionStringBuilder(_elevatedConnectionString) { InitialCatalog = "master" };
        try
        {
            ExecuteNonQuery(masterBuilder.ConnectionString,
                $"IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = '{_restrictedLogin}') DROP LOGIN [{_restrictedLogin}];");
        }
        catch { /* best-effort cleanup */ }
    }

    private void EnsureSchemaExists(string schemaName) =>
        ExecuteNonQuery(_elevatedConnectionString, $@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
    EXEC('CREATE SCHEMA [{schemaName}]')");

    private void DropSchemaIfExists(string schemaName) =>
        ExecuteNonQuery(_elevatedConnectionString, $@"
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
    DROP SCHEMA [{schemaName}]");

    private void DropAnyExistingTable(string tableName, string schemaName) =>
        ExecuteNonQuery(_elevatedConnectionString,
            $"DROP TABLE IF EXISTS [{schemaName}].[{tableName}]");

    private bool TableExistsInSchema(string tableName, string schemaName)
    {
        using var connection = new SqlConnection(_elevatedConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(1) FROM sys.tables t " +
            "INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
            "WHERE t.name = @TableName AND s.name = @SchemaName";
        command.Parameters.AddWithValue("@TableName", tableName);
        command.Parameters.AddWithValue("@SchemaName", schemaName);
        return (int)command.ExecuteScalar()! > 0;
    }

    // Counts this tenant's history rows in the named physical schema's history table. Tolerates an
    // absent table (returns 0) so the per-schema absence assertion works after rollback. Runs under
    // elevated creds so the test can read what the restricted principal cannot.
    private int GetHistoryRowCountInSchema(string physicalSchema)
    {
        using var connection = new SqlConnection(_elevatedConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"IF OBJECT_ID('[{physicalSchema}].[__BrighterMigrationHistory]', 'U') IS NULL " +
            "SELECT 0; " +
            $"ELSE SELECT COUNT(1) FROM [{physicalSchema}].[__BrighterMigrationHistory] " +
            "WHERE [BoxTableName] = @BoxTableName AND [SchemaName] = @SchemaName;";
        command.Parameters.AddWithValue("@BoxTableName", _tableName);
        command.Parameters.AddWithValue("@SchemaName", _schemaName);
        return (int)command.ExecuteScalar()!;
    }

    private void DeleteDboHistoryRows() =>
        ExecuteNonQuery(_elevatedConnectionString,
            "IF OBJECT_ID('[dbo].[__BrighterMigrationHistory]', 'U') IS NOT NULL " +
            $"DELETE FROM [dbo].[__BrighterMigrationHistory] WHERE [BoxTableName] = '{_tableName}'");

    private static void ExecuteNonQuery(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        try
        {
            DropRestrictedLogin();
            DropAnyExistingTable(_tableName, _schemaName);
            DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);
            DropAnyExistingTable(_tableName, "dbo");
            DeleteDboHistoryRows();
            DropSchemaIfExists(_schemaName);
        }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
