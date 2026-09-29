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

    [Test]
    [MethodDataSource(nameof(WriteModes))]
    public async Task When_sqlite_outbox_builder_receives_duplicate_ids_should_store_one_original_message(
        bool binary, bool useAsync, bool useBulk, bool changeCase)
    {
        //Arrange
        Execute(SqliteOutboxBuilder.GetDDL(TABLE, binary));
        var outbox = new SqliteOutbox(CreateConfiguration(binary));
        var original = CreateMessage("message-a", "original");
        var duplicate = CreateMessage(changeCase ? "MESSAGE-A" : "message-a", "replacement");

        //Act
        var exception = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            await AddAsync(outbox, original, useAsync, useBulk);
            await AddAsync(outbox, duplicate, useAsync, useBulk);
        });

        //Assert
        await Assert.That(exception).IsNull();
        await AssertSingleOriginal(outbox, original.Id);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
        var exception = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            if (useAsync)
                await outbox.AddAsync(batch, _context);
            else
                outbox.Add(batch, _context);
        });

        //Assert
        await Assert.That(exception).IsNull();
        await Assert.That(RowCount()).IsEqualTo(2L);
        var messages = outbox.Get(new[] { new Id("message-a"), new Id("message-b") }, _context).ToArray();
        await Assert.That(messages.Length).IsEqualTo(2);
        await Assert.That((await Assert.That(System.Linq.Enumerable.Where(messages, message => message.Id == new Id("message-a"))).HasSingleItem()).Body.Value).IsEqualTo("original");
        await Assert.That((await Assert.That(System.Linq.Enumerable.Where(messages, message => message.Id == new Id("message-b"))).HasSingleItem()).Body.Value).IsEqualTo("new");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
        await AssertSingleOriginal(outbox, new Id("message-a"));
        await Assert.That(await CurrentVersionAsync(configuration)).IsEqualTo(9);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
        await AssertSingleOriginal(outbox, new Id("message-a"));
        await Assert.That(await CurrentVersionAsync(configuration)).IsEqualTo(9);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
        var exception = await TestExceptionRecorder.CaptureAsync(() => provisioner.ProvisionAsync());

        //Assert
        await Assert.That((await Assert.That(exception).IsTypeOf<SqliteException>()).SqliteErrorCode).IsEqualTo(19);
        await Assert.That(RowCount()).IsEqualTo(2L);
        var messages = outbox.Get(new[] { new Id("message-a") }, _context).ToArray();
        await Assert.That(messages.Select(message => message.Body.Value).OrderBy(body => body).ToArray()).IsEqualTo(new[] { "conflicting payload", "original" });
        var detection = new SqliteBoxDetectionHelper();
        await Assert.That(await detection.DoesHistoryExistAsync(_connection, TABLE, null, null)).IsEqualTo(hasHistory);
        if (hasHistory)
            await Assert.That(await detection.GetMaxVersionAsync(_connection, TABLE, null, null)).IsEqualTo(8);

        Execute("DELETE FROM Messages WHERE MessageId COLLATE BINARY = 'MESSAGE-A';");
        await provisioner.ProvisionAsync();
        await provisioner.ProvisionAsync();
        await outbox.AddAsync(CreateMessage("MESSAGE-A", "replacement"), _context);
        await AssertSingleOriginal(outbox, new Id("message-a"));
        await Assert.That(await CurrentVersionAsync(configuration)).IsEqualTo(9);
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

    [Test]
    [MethodDataSource(nameof(ExistingIndexes))]
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
        await Assert.That(detectedVersion).IsEqualTo(protectsMessageId ? 9 : 8);
        await Assert.That(await CurrentVersionAsync(configuration)).IsEqualTo(9);
        await Assert.That(RowCount()).IsEqualTo(1L);
        await Assert.That(outbox.Get(message.Id, _context).Body.Value).IsEqualTo("original");
        await Assert.That(outbox.OutstandingMessages(TimeSpan.Zero, _context)).IsEmpty();
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_index_list('Messages');";
        await Assert.That((long)command.ExecuteScalar()!).IsEqualTo(protectsMessageId ? 2L : 3L);
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

    private async Task AssertSingleOriginal(SqliteOutbox outbox, Id id)
    {
        await Assert.That(RowCount()).IsEqualTo(1L);
        await Assert.That((await Assert.That(outbox.Get(new[] { id }, _context)).HasSingleItem()).Body.Value).IsEqualTo("original");
        await Assert.That((await Assert.That(outbox.OutstandingMessages(TimeSpan.FromMinutes(1), _context)).HasSingleItem()).Body.Value).IsEqualTo("original");
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
