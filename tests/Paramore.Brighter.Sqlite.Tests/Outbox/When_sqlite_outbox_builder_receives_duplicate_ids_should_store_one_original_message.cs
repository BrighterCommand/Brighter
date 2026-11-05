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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class SqliteOutboxMessageIdUniquenessTests : IDisposable
{
    private const string TABLE = "Messages";
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;
    private readonly RequestContext _context = new();

    public SqliteOutboxMessageIdUniquenessTests()
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"message_ids_{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
    }

    public static IEnumerable<object[]> WriteModes()
    {
        foreach (var binary in new[] { false, true })
        foreach (var useAsync in new[] { false, true })
        foreach (var useBulk in new[] { false, true })
        foreach (var changeCase in new[] { false, true })
            yield return new object[] { binary, useAsync, useBulk, changeCase };
    }

    [Theory]
    [MemberData(nameof(WriteModes))]
    public async Task When_sqlite_outbox_builder_receives_duplicate_ids_should_store_one_original_message(
        bool binary, bool useAsync, bool useBulk, bool changeCase)
    {
        //Arrange
        Execute(SqliteOutboxBuilder.GetDDL(TABLE, binary));
        var outbox = new SqliteOutbox(CreateConfiguration(binary));
        var original = CreateMessage("message-a", "original");
        var duplicate = CreateMessage(changeCase ? "MESSAGE-A" : "message-a", "replacement");

        //Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await AddAsync(outbox, original, useAsync, useBulk);
            await AddAsync(outbox, duplicate, useAsync, useBulk);
        });

        //Assert
        Assert.Null(exception);
        AssertSingleOriginal(outbox, original.Id);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_sqlite_bulk_add_contains_duplicate_ids_should_preserve_each_distinct_message(
        bool binary, bool useAsync)
    {
        //Arrange
        Execute(SqliteOutboxBuilder.GetDDL(TABLE, binary));
        var outbox = new SqliteOutbox(CreateConfiguration(binary));
        outbox.Add(CreateMessage("message-a", "original"), _context);
        var batch = new[]
        {
            CreateMessage("MESSAGE-A", "replacement"),
            CreateMessage("message-b", "new"),
            CreateMessage("MESSAGE-B", "second replacement")
        };

        //Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            if (useAsync)
                await outbox.AddAsync(batch, _context);
            else
                outbox.Add(batch, _context);
        });

        //Assert
        Assert.Null(exception);
        Assert.Equal(2L, RowCount());
        var messages = outbox.Get(new[] { new Id("message-a"), new Id("message-b") }, _context).ToArray();
        Assert.Equal(2, messages.Length);
        Assert.Equal("original", Assert.Single(messages, message => message.Id == new Id("message-a")).Body.Value);
        Assert.Equal("new", Assert.Single(messages, message => message.Id == new Id("message-b")).Body.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_sqlite_outbox_is_provisioned_from_empty_database_should_enforce_message_id_uniqueness(bool binary)
    {
        //Arrange
        var configuration = CreateConfiguration(binary);
        var provisioner = CreateProvisioner(configuration);

        //Act
        await provisioner.ProvisionAsync();
        await provisioner.ProvisionAsync();
        var outbox = new SqliteOutbox(configuration);
        outbox.Add(CreateMessage("message-a", "original"), _context);
        await outbox.AddAsync(CreateMessage("MESSAGE-A", "replacement"), _context);

        //Assert
        AssertSingleOriginal(outbox, new Id("message-a"));
        Assert.Equal(9, await CurrentVersionAsync(configuration));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_upgrading_a_legacy_sqlite_outbox_should_preserve_rows_and_enforce_message_id_uniqueness(
        bool binary, bool hasHistory)
    {
        //Arrange
        CreateLegacyTable(binary, hasHistory);
        var configuration = CreateConfiguration(binary);
        var outbox = new SqliteOutbox(configuration);
        outbox.Add(CreateMessage("message-a", "original"), _context);
        var provisioner = CreateProvisioner(configuration);

        //Act
        await provisioner.ProvisionAsync();
        await provisioner.ProvisionAsync();
        await outbox.AddAsync(CreateMessage("MESSAGE-A", "replacement"), _context);

        //Assert
        AssertSingleOriginal(outbox, new Id("message-a"));
        Assert.Equal(9, await CurrentVersionAsync(configuration));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_upgrading_a_sqlite_outbox_with_duplicate_ids_should_preserve_rows_and_allow_retry_after_resolution(
        bool binary, bool hasHistory)
    {
        //Arrange
        CreateLegacyTable(binary, hasHistory);
        var configuration = CreateConfiguration(binary);
        var outbox = new SqliteOutbox(configuration);
        outbox.Add(CreateMessage("message-a", "original"), _context);
        outbox.Add(CreateMessage("MESSAGE-A", "conflicting payload"), _context);
        var provisioner = CreateProvisioner(configuration);

        //Act
        var exception = await Record.ExceptionAsync(() => provisioner.ProvisionAsync());

        //Assert
        Assert.Equal(19, Assert.IsType<SqliteException>(exception).SqliteErrorCode);
        Assert.Equal(2L, RowCount());
        var messages = outbox.Get(new[] { new Id("message-a") }, _context).ToArray();
        Assert.Equal(new[] { "conflicting payload", "original" },
            messages.Select(message => message.Body.Value).OrderBy(body => body).ToArray());
        var detection = new SqliteBoxDetectionHelper();
        Assert.Equal(hasHistory, await detection.DoesHistoryExistAsync(_connection, TABLE, null, null));
        if (hasHistory)
            Assert.Equal(8, await detection.GetMaxVersionAsync(_connection, TABLE, null, null));

        Execute("DELETE FROM Messages WHERE MessageId COLLATE BINARY = 'MESSAGE-A';");
        await provisioner.ProvisionAsync();
        await provisioner.ProvisionAsync();
        await outbox.AddAsync(CreateMessage("MESSAGE-A", "replacement"), _context);
        AssertSingleOriginal(outbox, new Id("message-a"));
        Assert.Equal(9, await CurrentVersionAsync(configuration));
    }

    private RelationalDatabaseConfiguration CreateConfiguration(bool binary)
        => new(_connectionString, outBoxTableName: TABLE, binaryMessagePayload: binary);

    public static IEnumerable<object[]> ExistingIndexes()
    {
        foreach (var hasHistory in new[] { false, true })
        {
            yield return new object[] { "CREATE INDEX custom_index ON Messages (MessageId);", false, hasHistory };
            yield return new object[] { "CREATE UNIQUE INDEX custom_index ON Messages (MessageId COLLATE BINARY);", false, hasHistory };
            yield return new object[] { "CREATE UNIQUE INDEX custom_index ON Messages (MessageId, Topic);", false, hasHistory };
            yield return new object[] { "CREATE UNIQUE INDEX custom_index ON Messages (MessageId) WHERE Dispatched IS NULL;", false, hasHistory };
            yield return new object[] { "CREATE UNIQUE INDEX custom_index ON Messages (lower(MessageId));", false, hasHistory };
            yield return new object[] { "CREATE UNIQUE INDEX custom_index ON Messages (MessageId COLLATE NOCASE);", true, hasHistory };
        }
    }

    [Theory]
    [MemberData(nameof(ExistingIndexes))]
    public async Task When_a_legacy_outbox_has_an_existing_index_should_require_full_case_insensitive_message_id_uniqueness(
        string indexDdl, bool protectsMessageId, bool hasHistory)
    {
        //Arrange
        CreateLegacyTable(binary: false, hasHistory: hasHistory);
        Execute(indexDdl);
        var configuration = CreateConfiguration(binary: false);
        var outbox = new SqliteOutbox(configuration);
        var message = CreateMessage("message-a", "original");
        outbox.Add(message, _context);
        outbox.MarkDispatched(message.Id, _context);

        //Act
        var detectedVersion = await CurrentVersionAsync(configuration);
        await CreateProvisioner(configuration).ProvisionAsync();
        await outbox.AddAsync(CreateMessage("MESSAGE-A", "replacement"), _context);

        //Assert
        Assert.Equal(protectsMessageId ? 9 : 8, detectedVersion);
        Assert.Equal(9, await CurrentVersionAsync(configuration));
        Assert.Equal(1L, RowCount());
        Assert.Equal("original", outbox.Get(message.Id, _context).Body.Value);
        Assert.Empty(outbox.OutstandingMessages(TimeSpan.Zero, _context));
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_index_list('Messages');";
        Assert.Equal(protectsMessageId ? 2L : 3L, (long)command.ExecuteScalar()!);
    }

    private static SqliteOutboxProvisioner CreateProvisioner(RelationalDatabaseConfiguration configuration)
    {
        var catalog = new SqliteOutboxMigrationCatalog();
        var runner = new SqliteBoxMigrationRunner(catalog, configuration, TimeSpan.FromSeconds(5), enableWalMode: false);
        return new SqliteOutboxProvisioner(new SqliteBoxDetectionHelper(), catalog,
            new SqlitePayloadModeValidator(), configuration, runner);
    }

    private async Task<int> CurrentVersionAsync(RelationalDatabaseConfiguration configuration)
        => await new SqliteBoxDetectionHelper().DetectCurrentVersionAsync(_connection, TABLE, null,
            BoxType.Outbox, new SqliteOutboxMigrationCatalog().All(configuration));

    private static Message CreateMessage(string id, string body)
        => new(new MessageHeader(new Id(id), new RoutingKey("test-topic"), MessageType.MT_EVENT,
            timeStamp: DateTimeOffset.UtcNow.AddMinutes(-5)), new MessageBody(body));

    private async Task AddAsync(SqliteOutbox outbox, Message message, bool useAsync, bool useBulk)
    {
        if (useAsync)
        {
            if (useBulk)
                await outbox.AddAsync(new[] { message }, _context);
            else
                await outbox.AddAsync(message, _context);
        }
        else if (useBulk)
            outbox.Add(new[] { message }, _context);
        else
            outbox.Add(message, _context);
    }

    private void AssertSingleOriginal(SqliteOutbox outbox, Id id)
    {
        Assert.Equal(1L, RowCount());
        Assert.Equal("original", Assert.Single(outbox.Get(new[] { id }, _context)).Body.Value);
        Assert.Equal("original", Assert.Single(outbox.OutstandingMessages(TimeSpan.FromMinutes(1), _context)).Body.Value);
    }

    private long RowCount()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Messages;";
        return (long)command.ExecuteScalar()!;
    }

    private void CreateLegacyTable(bool binary, bool hasHistory)
    {
        Execute($"""
            CREATE TABLE Messages (
                MessageId TEXT NOT NULL COLLATE NOCASE,
                MessageType TEXT NULL, Topic TEXT NULL, Timestamp TEXT NULL,
                CorrelationId TEXT NULL, ReplyTo TEXT NULL, ContentType TEXT NULL,
                PartitionKey TEXT NULL, WorkflowId TEXT NULL, JobId TEXT NULL,
                Dispatched TEXT NULL, HeaderBag TEXT NULL, Body {(binary ? "BLOB" : "TEXT")} NULL,
                Source TEXT NULL, Type TEXT NULL, DataSchema TEXT NULL, Subject TEXT NULL,
                TraceParent TEXT NULL, TraceState TEXT NULL, Baggage TEXT NULL,
                DataRef TEXT NULL, CausationId TEXT NULL, SpecVersion TEXT NULL
            );
            CREATE INDEX idx_Messages_CausationId ON Messages (CausationId);
            """);
        if (hasHistory)
        {
            Execute("""
                CREATE TABLE __BrighterMigrationHistory (
                    MigrationVersion INTEGER NOT NULL,
                    BoxTableName TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    AppliedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    PRIMARY KEY (BoxTableName, MigrationVersion)
                );
                INSERT INTO __BrighterMigrationHistory (MigrationVersion, BoxTableName, Description)
                VALUES (8, 'Messages', 'fresh install at V8');
                """);
        }
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
