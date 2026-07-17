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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MSSQL.Tests.BoxProvisioning;

// Spec 0029 NF5/AC7 (ADR 0060 D6): each provisioning run must record the resolved history schema
// and the active MigrationHistoryScope at Information level, and when the D5 seed runs (the
// Global → PerSchema flip) it must emit a distinct Information log surfacing the copied row
// count plus legacy + target schemas as separate structured fields, together with the row count
// as an attribute on the existing legacy-history-seeded Activity event so a trace-store query can
// filter by it. Two facts share this class because the seed scenario reuses the per-run log
// machinery and the normal-run log must NOT carry the seed fields.
public class MsSqlBoxMigrationRunnerLoggingTests
{
    private readonly string _connectionString = Configuration.DefaultConnectingString;
    private readonly string _tableName = $"test_outbox_{Guid.NewGuid():N}";
    private readonly string _schemaName = $"billing_obs_{Guid.NewGuid():N}";

    [Test]
    public async Task When_provisioning_runs_mssql_runner_should_log_resolved_history_schema_and_scope()
    {
        //Arrange — clean slate, PerSchema scope with a non-null SchemaName. No legacy history rows
        //exist so the D5 seed does NOT fire on this run; this isolates the per-run resolved-schema
        //log from the seed-only structured log asserted in the second test. The capturing logger
        //records each Log<TState> call's TState alongside the formatted message so the test can
        //assert on the structured field values rather than the message wording — a copy-edit of
        //the message template will not break the contract.
        Configuration.EnsureDatabaseExists(_connectionString);
        EnsureSchemaExists(_schemaName);
        DropAnyExistingTable(_tableName, _schemaName);
        DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);

        var config = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var capturingLogger = new StructuredCapturingLogger();
        var runner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), config, TimeSpan.FromSeconds(30),
            logger: capturingLogger,
            scope: MigrationHistoryScope.PerSchema);
        var provisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            config,
            runner);

        //Act
        await provisioner.ProvisionAsync();

        //Assert — a single Information log surfaces the resolved history schema and the active scope
        //as structured fields keyed by BoxTable / HistorySchema / Scope. The base runner is the only
        //place these three fields co-occur, so the predicate uniquely identifies the per-run log.
        var resolvedSchemaLog = capturingLogger.Entries.SingleOrDefault(e =>
            e.Level == LogLevel.Information
            && e.HasField("BoxTable")
            && e.HasField("HistorySchema")
            && e.HasField("Scope"));
        await Assert.That(resolvedSchemaLog).IsNotNull();

        //Assert — the structured field values match the actual run's configuration. For PerSchema on
        //MSSQL, HistorySchema must be the configured SchemaName (not the backend default "dbo") and
        //Scope must carry the enum value PerSchema, so an operator filtering a structured log sink
        //by Scope=PerSchema and HistorySchema=<tenant> can locate this tenant's provisioning event.
        await Assert.That(resolvedSchemaLog!.Field("BoxTable")?.ToString()).IsEqualTo(_tableName);
        await Assert.That(resolvedSchemaLog.Field("HistorySchema")?.ToString()).IsEqualTo(_schemaName);
        await Assert.That(resolvedSchemaLog.Field("Scope")).IsEqualTo(MigrationHistoryScope.PerSchema);
    }

    [Test]
    public async Task When_seed_runs_during_global_to_per_schema_flip_runner_should_log_row_count_and_emit_activity_attribute()
    {
        //Arrange — provision once under Global so the legacy dbo history table contains exactly one
        //tenant row, then flip to PerSchema. The flip's D5 seed inside EnsureHistoryTableAsync must
        //emit a distinct Information log carrying RowCount + BoxTable + LegacySchema + TargetSchema
        //as separate structured fields (reviewer #3) so a structured sink can filter on each, and
        //must add the row count as an attribute on the existing legacy-history-seeded Activity event
        //so a trace-store query can filter on it without parsing event names.
        Configuration.EnsureDatabaseExists(_connectionString);
        EnsureSchemaExists(_schemaName);
        DropAnyExistingTable(_tableName, _schemaName);
        DropAnyExistingTable("__BrighterMigrationHistory", _schemaName);
        DeleteDboHistoryRows();

        var globalConfig = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var globalRunner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), globalConfig, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.Global);
        var globalProvisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            globalConfig,
            globalRunner);
        await globalProvisioner.ProvisionAsync();

        // Sanity-check the arranged precondition so a regression in the Global path can't masquerade
        // as a missing seed log: exactly one tenant row in dbo and no per-schema history table yet.
        await Assert.That(GetHistoryRowCountInSchema("dbo")).IsEqualTo(1);
        await Assert.That(TableExistsInSchema("__BrighterMigrationHistory", _schemaName)).IsFalse();

        var capturingLogger = new StructuredCapturingLogger();
        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BrighterSemanticConventions.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => capturedActivities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        var tracer = new BrighterTracer();

        var perSchemaConfig = new RelationalDatabaseConfiguration(
            _connectionString,
            outBoxTableName: _tableName,
            schemaName: _schemaName);
        var perSchemaRunner = new MsSqlBoxMigrationRunner(
            new MsSqlOutboxMigrationCatalog(), perSchemaConfig, TimeSpan.FromSeconds(30),
            logger: capturingLogger,
            tracer: tracer,
            scope: MigrationHistoryScope.PerSchema);
        var perSchemaProvisioner = new MsSqlOutboxProvisioner(
            new MsSqlBoxDetectionHelper(),
            new MsSqlOutboxMigrationCatalog(),
            new MsSqlPayloadModeValidator(),
            perSchemaConfig,
            perSchemaRunner);

        //Act
        await perSchemaProvisioner.ProvisionAsync();

        //Assert — the seed Information log surfaces RowCount + BoxTable + LegacySchema + TargetSchema
        //as separate structured fields. The four-field predicate uniquely identifies the seed log;
        //the per-run resolved-schema log does NOT carry these keys, so we cannot accidentally match it.
        var seedLog = capturingLogger.Entries.SingleOrDefault(e =>
            e.Level == LogLevel.Information
            && e.HasField("RowCount")
            && e.HasField("BoxTable")
            && e.HasField("LegacySchema")
            && e.HasField("TargetSchema"));
        await Assert.That(seedLog).IsNotNull();
        await Assert.That(Convert.ToInt32(seedLog!.Field("RowCount"))).IsEqualTo(1);
        await Assert.That(seedLog.Field("BoxTable")?.ToString()).IsEqualTo(_tableName);
        await Assert.That(seedLog.Field("LegacySchema")?.ToString()).IsEqualTo("dbo");
        await Assert.That(seedLog.Field("TargetSchema")?.ToString()).IsEqualTo(_schemaName);

        //Assert — the legacy-history-seeded Activity event on the migration span carries the row
        //count as the attribute "brighter.box.migration.seed.rows". Without this attribute, a trace
        //consumer would know the seed fired but not how many rows it copied — the inverse of what
        //we want for capacity / flip-impact dashboards. The literal attribute key is asserted
        //against the spec string (ADR 0060 D6) rather than a constant so the test pins the wire
        //contract; the implementation may introduce a named constant for the same string.
        var seedEvent = capturedActivities
            .SelectMany(a => a.Events)
            .Where(e => e.Name == BrighterSemanticConventions.BoxMigrationEventLegacyHistorySeeded)
            .ToList();
        await Assert.That(seedEvent).HasSingleItem();
        var rowsAttribute = seedEvent[0].Tags.FirstOrDefault(t => t.Key == "brighter.box.migration.seed.rows");
        await Assert.That(rowsAttribute.Key).IsNotNull();
        await Assert.That(Convert.ToInt32(rowsAttribute.Value)).IsEqualTo(1);
    }

    private void EnsureSchemaExists(string schemaName) =>
        ExecuteNonQuery($@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
    EXEC('CREATE SCHEMA [{schemaName}]')");

    private void DropSchemaIfExists(string schemaName) =>
        ExecuteNonQuery($@"
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
    DROP SCHEMA [{schemaName}]");

    private void DropAnyExistingTable(string tableName, string schemaName) =>
        ExecuteNonQuery($"DROP TABLE IF EXISTS [{schemaName}].[{tableName}]");

    private bool TableExistsInSchema(string tableName, string schemaName)
    {
        using var connection = new SqlConnection(_connectionString);
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
    // absent table (returns 0) because the per-schema history may not exist before the flip and the
    // dbo table may not exist before any Global-scope test has run on this database.
    private int GetHistoryRowCountInSchema(string physicalSchema)
    {
        using var connection = new SqlConnection(_connectionString);
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
        ExecuteNonQuery(
            "IF OBJECT_ID('[dbo].[__BrighterMigrationHistory]', 'U') IS NOT NULL " +
            $"DELETE FROM [dbo].[__BrighterMigrationHistory] WHERE [BoxTableName] = '{_tableName}'");

    private void ExecuteNonQuery(string sql)
    {
        using var connection = new SqlConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    [Before(Test)]
    public Task InitializeAsync() => Task.CompletedTask;

    [After(Test)]
    public Task DisposeAsync()
    {
        try
        {
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

// Minimal ILogger that captures each Log<TState> call's TState alongside the formatted message so
// tests can assert on structured-field values (the placeholders in the message template) instead
// of the message wording. Microsoft.Extensions.Logging materialises the message-template state as
// an IReadOnlyList<KeyValuePair<string, object?>> (FormattedLogValues), which is the canonical way
// to read the placeholder name → value pairs. Inlined as a `file` class so the broader test-double
// surface in TestDoubles/CapturingLogger.cs (which carries Level/Message/Exception only) is not
// disturbed by an additional concern.
file sealed class StructuredCapturingLogger : ILogger
{
    public List<StructuredCapturedEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var fields = state as IReadOnlyList<KeyValuePair<string, object?>>;
        Entries.Add(new StructuredCapturedEntry(
            logLevel, formatter(state, exception), exception, fields));
    }
}

file sealed record StructuredCapturedEntry(
    LogLevel Level,
    string Message,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>>? Fields)
{
    public bool HasField(string name) =>
        Fields is not null && Fields.Any(kv => kv.Key == name);

    public object? Field(string name) =>
        Fields?.FirstOrDefault(kv => kv.Key == name).Value;
}