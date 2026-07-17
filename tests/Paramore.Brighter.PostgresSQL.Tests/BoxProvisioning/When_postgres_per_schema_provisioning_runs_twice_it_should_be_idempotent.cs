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
using Npgsql;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

// Spec 0029 FR5/NF2/AC3 (ADR 0060 D5): a second PerSchema provisioning run is idempotent. The
// detection-driven short-circuit (T4) reads the per-schema __BrighterMigrationHistory, sees the
// box already at the latest version, and applies NO migration — so the per-schema history row
// count is unchanged and no duplicate rows appear. Asserting the AppliedAt timestamp is also
// unchanged proves the existing row was not deleted-and-reinserted (a count check alone would
// not distinguish that from a true no-op). PG folds unquoted identifiers to lowercase, so the
// schema/table are addressed and filtered in their folded form.
public class PostgreSqlPerSchemaIdempotencyTests
{
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_idem_{Guid.NewGuid():N}";
    private readonly string _foldedSchema;
    private readonly PostgreSqlOutboxProvisioner _provisioner;

    public PostgreSqlPerSchemaIdempotencyTests()
    {
        _foldedSchema = _schemaName.ToLowerInvariant();

        // Evident data: PerSchema scope + a non-null SchemaName is the placement case under test.
        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var runner = new PostgreSqlBoxMigrationRunner(
            new PostgreSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
        _provisioner = new PostgreSqlOutboxProvisioner(
            new PostgreSqlBoxDetectionHelper(),
            new PostgreSqlOutboxMigrationCatalog(),
            new PostgreSqlPayloadModeValidator(),
            config,
            runner);
    }

    [Test]
    public async Task When_per_schema_provisioning_runs_twice_it_should_apply_no_migrations_and_not_duplicate_history()
    {
        //Arrange — operator pre-creates the (folded) schema; runner does not create schemas itself.
        new PostgresSqlTestHelper().SetupDatabase();
        await EnsureSchemaExistsAsync(_foldedSchema);
        await DropAnyExistingTableAsync(_tableName, _foldedSchema);
        await DropAnyExistingTableAsync("__BrighterMigrationHistory", _foldedSchema);

        //Act — first fresh-install run under PerSchema.
        var firstException = await TestExceptionRecorder.CaptureAsync(() => _provisioner.ProvisionAsync());

        //Assert — one history row written to the per-schema table; capture its AppliedAt.
        await Assert.That(firstException).IsNull();
        await Assert.That(await GetHistoryRowCountAsync()).IsEqualTo(1);
        var appliedAtAfterFirstRun = await GetSingleHistoryAppliedAtAsync();

        //Act — second run re-detects from the per-schema history; should apply nothing.
        var secondException = await TestExceptionRecorder.CaptureAsync(() => _provisioner.ProvisionAsync());

        //Assert — idempotent: still exactly one row, and the original row is untouched (same
        //AppliedAt), proving no migration was re-applied and no row was rewritten.
        await Assert.That(secondException).IsNull();
        await Assert.That(await GetHistoryRowCountAsync()).IsEqualTo(1);
        await Assert.That(await GetSingleHistoryAppliedAtAsync()).IsEqualTo(appliedAtAfterFirstRun);
    }

    private async Task EnsureSchemaExistsAsync(string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"CREATE SCHEMA IF NOT EXISTS ""{schemaName}""";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropAnyExistingTableAsync(string tableName, string schemaName)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"DROP TABLE IF EXISTS ""{schemaName}"".""{tableName}""";
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> GetHistoryRowCountAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT COUNT(1) FROM ""{_foldedSchema}"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @BoxSchemaName";
        command.Parameters.AddWithValue("@BoxTableName", _tableName.ToLowerInvariant());
        command.Parameters.AddWithValue("@BoxSchemaName", _foldedSchema);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    // Returns the raw AppliedAt scalar (TIMESTAMPTZ). Kept as object so the comparison is agnostic
    // about Npgsql's CLR mapping — both reads return the same type, so Assert.Equal compares fine.
    private async Task<object> GetSingleHistoryAppliedAtAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $@"
SELECT ""AppliedAt"" FROM ""{_foldedSchema}"".""__BrighterMigrationHistory""
WHERE ""BoxTableName"" = @BoxTableName AND ""SchemaName"" = @BoxSchemaName";
        command.Parameters.AddWithValue("@BoxTableName", _tableName.ToLowerInvariant());
        command.Parameters.AddWithValue("@BoxSchemaName", _foldedSchema);
        return (await command.ExecuteScalarAsync())!;
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var dropSchemaCmd = connection.CreateCommand();
            dropSchemaCmd.CommandText = $@"DROP SCHEMA IF EXISTS ""{_foldedSchema}"" CASCADE";
            await dropSchemaCmd.ExecuteNonQueryAsync();
            await using var dropPublicTable = connection.CreateCommand();
            dropPublicTable.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{_tableName}""";
            await dropPublicTable.ExecuteNonQueryAsync();
        }
        catch { /* best-effort cleanup */ }
    }
}
