#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.TestDoubles;

internal sealed class MsSqlMessageIdOutboxStore : IAsyncDisposable
{
    public RelationalDatabaseConfiguration Config { get; }
    public RelationalDatabaseConfiguration OutboxConfig => new(Config.ConnectionString,
        outBoxTableName: QualifiedTable, binaryMessagePayload: Config.BinaryMessagePayload);
    public string Schema { get; }
    public string Table { get; } = $"outbox_{Guid.NewGuid():N}";
    public string QualifiedTable => $"[{Schema}].[{Table}]";

    public MsSqlMessageIdOutboxStore(bool binary, bool customSchema = false)
    {
        Schema = customSchema ? $"message_id_{Guid.NewGuid():N}" : "dbo";
        Config = new RelationalDatabaseConfiguration(Configuration.DefaultConnectingString,
            outBoxTableName: Table, binaryMessagePayload: binary,
            schemaName: customSchema ? Schema : null);
    }

    public async Task InitializeAsync(bool legacy = false, bool withHistory = false, bool provision = false)
    {
        await Configuration.EnsureDatabaseExistsAsync(Config.ConnectionString);
        if (Schema != "dbo")
            await ExecuteAsync($"CREATE SCHEMA [{Schema}]");

        if (provision)
            await ProvisionAsync();
        else
            await ExecuteAsync(legacy ? LegacyDdl() : SqlOutboxBuilder.GetDDL(Table, Config.BinaryMessagePayload, Config.SchemaName));

        if (withHistory)
            await SeedV8HistoryAsync();
    }

    public Task ProvisionAsync()
    {
        var catalog = new MsSqlOutboxMigrationCatalog();
        var runner = new MsSqlBoxMigrationRunner(catalog, Config, TimeSpan.FromSeconds(30),
            scope: MigrationHistoryScope.PerSchema);
        return new MsSqlOutboxProvisioner(new MsSqlBoxDetectionHelper(), catalog,
            new MsSqlPayloadModeValidator(), Config, runner).ProvisionAsync();
    }

    public static Message Message(Id id, string body) => new(
        new MessageHeader(id, new RoutingKey("message-id-test"), MessageType.MT_COMMAND,
            timeStamp: DateTimeOffset.UtcNow.AddMinutes(-10)), new MessageBody(body));

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqlConnection(Config.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> ScalarAsync(string sql)
    {
        await using var connection = new SqlConnection(Config.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public Task<int> RowCountAsync() => ScalarAsync($"SELECT COUNT(*) FROM {QualifiedTable}");

    public Task<int> LatestVersionAsync() => ScalarAsync($"""
        IF OBJECT_ID(N'[{Schema}].[__BrighterMigrationHistory]') IS NULL
            SELECT 0;
        ELSE
            SELECT COALESCE(MAX([MigrationVersion]), 0)
            FROM [{Schema}].[__BrighterMigrationHistory]
            WHERE [SchemaName] = '{Schema}' AND [BoxTableName] = '{Table}';
        """);

    public Task<int> MessageIdUniqueIndexesAsync() => ScalarAsync($"""
        SELECT COUNT(*) FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID(N'{QualifiedTable}')
          AND i.is_unique = 1 AND i.is_disabled = 0 AND i.has_filter = 0
          AND (SELECT COUNT(*) FROM sys.index_columns ic
               WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0) = 1
          AND EXISTS (SELECT 1 FROM sys.index_columns ic
                      JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                      WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                        AND ic.key_ordinal = 1 AND c.name = 'MessageId');
        """);

    private Task SeedV8HistoryAsync() => ExecuteAsync($"""
        CREATE TABLE [{Schema}].[__BrighterMigrationHistory] (
            [MigrationVersion] INT NOT NULL,
            [SchemaName] VARCHAR(256) NOT NULL,
            [BoxTableName] VARCHAR(256) NOT NULL,
            [Description] NVARCHAR(512) NOT NULL,
            [AppliedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
            PRIMARY KEY ([SchemaName], [BoxTableName], [MigrationVersion])
        );
        INSERT INTO [{Schema}].[__BrighterMigrationHistory]
            ([MigrationVersion], [SchemaName], [BoxTableName], [Description])
        VALUES (8, '{Schema}', '{Table}', 'fresh install at V8');
        """);

    private string LegacyDdl() => $"""
        CREATE TABLE {QualifiedTable} (
            [Id] BIGINT NOT NULL IDENTITY PRIMARY KEY,
            [MessageId] NVARCHAR(255) NOT NULL,
            [Topic] NVARCHAR(255) NULL,
            [MessageType] NVARCHAR(32) NULL,
            [Timestamp] DATETIME NULL,
            [CorrelationId] NVARCHAR(255) NULL,
            [ReplyTo] NVARCHAR(255) NULL,
            [ContentType] NVARCHAR(128) NULL,
            [PartitionKey] NVARCHAR(255) NULL,
            [WorkflowId] NVARCHAR(255) NULL,
            [JobId] NVARCHAR(255) NULL,
            [Dispatched] DATETIME NULL,
            [HeaderBag] NVARCHAR(MAX) NULL,
            [Body] {(Config.BinaryMessagePayload ? "VARBINARY(MAX)" : "NVARCHAR(MAX)")} NULL,
            [Source] NVARCHAR(255) NULL,
            [Type] NVARCHAR(255) NULL,
            [DataSchema] NVARCHAR(255) NULL,
            [Subject] NVARCHAR(255) NULL,
            [TraceParent] NVARCHAR(255) NULL,
            [TraceState] NVARCHAR(255) NULL,
            [Baggage] NVARCHAR(MAX) NULL,
            [DataRef] NVARCHAR(255) NULL,
            [CausationId] NVARCHAR(255) NULL,
            [SpecVersion] NVARCHAR(255) NULL
        );
        CREATE INDEX [idx_{Table}_CausationId] ON {QualifiedTable} ([CausationId]);
        """;

    public async ValueTask DisposeAsync()
    {
        await ExecuteAsync($"DROP TABLE IF EXISTS {QualifiedTable}");
        if (Schema != "dbo")
        {
            await ExecuteAsync($"DROP TABLE IF EXISTS [{Schema}].[__BrighterMigrationHistory]");
            await ExecuteAsync($"DROP SCHEMA [{Schema}]");
        }
    }
}
