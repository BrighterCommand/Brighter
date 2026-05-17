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
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.BoxProvisioning;

public class When_many_postgres_provisioners_race_to_create_history_table_they_should_all_complete : IAsyncLifetime
{
    private const int RacerCount = 16;
    private readonly string _connectionString = PostgreSqlSettings.TestsBrighterConnectionString;
    private readonly string[] _tableNames;

    public When_many_postgres_provisioners_race_to_create_history_table_they_should_all_complete()
    {
        _tableNames = Enumerable.Range(0, RacerCount)
            .Select(_ => $"test_outbox_{Guid.NewGuid():N}")
            .ToArray();
    }

    [Fact]
    public async Task Should_all_complete_without_transaction_abort()
    {
        // Arrange — 16 racers each provision a distinct outbox table, so each holds its own
        // per-table advisory lock and they DO NOT serialize on the box-table lock. The shared
        // history table __BrighterMigrationHistory is dropped first, so every racer races to
        // CREATE TABLE IF NOT EXISTS the same global history table. The CREATE is not atomic
        // at the Postgres catalog level: one racer wins; the others surface UniqueViolation /
        // DuplicateTable / DuplicateObject inside EnsureHistoryTableAsync. The catch swallows
        // those (the table now exists, which is the post-condition) — but Postgres ALSO marks
        // the outer transaction as aborted, so the next statement in the same transaction
        // (RedetectStateAsync) fails with 25P02 "current transaction is aborted".
        //
        // Expected behaviour: every racer completes, AND at least one swallow ActivityEvent
        // (BoxMigrationEventHistoryTableRaceSwallowed) was emitted somewhere.
        new PostgresSqlTestHelper().SetupDatabase();
        await DropHistoryTableAsync();

        var exportedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(BrighterSemanticConventions.SourceName)
            .AddInMemoryExporter(exportedActivities)
            .Build();
        var tracer = new BrighterTracer();

        var startGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act — every racer waits on a single gate so they all release together.
        var racers = _tableNames.Select(tableName => Task.Run(async () =>
        {
            await startGate.Task;
            var config = new RelationalDatabaseConfiguration(
                _connectionString, outBoxTableName: tableName);
            var runner = new PostgreSqlBoxMigrationRunner(
                config, TimeSpan.FromSeconds(30), tracer: tracer);
            var provisioner = new PostgreSqlOutboxProvisioner(config, runner);
            await provisioner.ProvisionAsync();
        })).ToArray();

        startGate.SetResult(true);

        // Assert — none of the racers should have thrown. Use WhenAll's aggregation so a single
        // failure surfaces a meaningful PostgresException rather than a "task faulted" abstraction.
        var aggregate = await Record.ExceptionAsync(() => Task.WhenAll(racers));
        Assert.Null(aggregate);

        // Force the OTel exporter to flush so completed activities land in exportedActivities.
        tracerProvider.ForceFlush();

        // At least one racer must have actually raced (i.e. lost to the CREATE) and emitted the
        // swallow event. If zero events fired, the racers were perfectly serialised by the
        // Postgres catalog and we did not exercise the bug path — the test is a no-op and we
        // would not detect a regression in the savepoint guard.
        var swallowEvents = exportedActivities
            .SelectMany(a => a.Events)
            .Count(e => e.Name == BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed);
        Assert.True(
            swallowEvents > 0,
            $"Expected at least one '{BrighterSemanticConventions.BoxMigrationEventHistoryTableRaceSwallowed}' " +
            $"event across {RacerCount} racers; got none. The race window did not fire.");
    }

    private async Task DropHistoryTableAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"DROP TABLE IF EXISTS ""public"".""__BrighterMigrationHistory""";
        await command.ExecuteNonQueryAsync();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            foreach (var tableName in _tableNames)
            {
                await using var drop = connection.CreateCommand();
                drop.CommandText = $@"DROP TABLE IF EXISTS ""public"".""{tableName}""";
                await drop.ExecuteNonQueryAsync();
            }
            await using var deleteHistory = connection.CreateCommand();
            deleteHistory.CommandText = @"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_schema = 'public' AND table_name = '__BrighterMigrationHistory') THEN
        DELETE FROM ""public"".""__BrighterMigrationHistory"" WHERE ""BoxTableName"" = ANY(@TableNames);
    END IF;
END
$$;";
            var param = deleteHistory.CreateParameter();
            param.ParameterName = "@TableNames";
            param.Value = _tableNames;
            deleteHistory.Parameters.Add(param);
            await deleteHistory.ExecuteNonQueryAsync();
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
