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

using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Storage;
using Paramore.Brighter.Transforms.Transformers;
using Xunit;

namespace Paramore.Brighter.Base.Test.Outbox;

/// <summary>
/// Exercises DataRef persistence and claim-check retrieval against relational outboxes.
/// </summary>
public abstract class RelationalOutboxDataRefTests : IAsyncLifetime
{
    private RelationalDatabaseConfiguration? _configuration;
    private bool _tableCreated;

    /// <summary>Gets the isolated table name for this test case.</summary>
    protected string TableName { get; } = $"dataref_{Uuid.New():N}";

    /// <summary>Gets the provider's test database connection string.</summary>
    protected abstract string ConnectionString { get; }

    /// <summary>Gets the statement adding a nullable DataRef column to the test table.</summary>
    protected abstract string AddDataRefSql { get; }

    /// <summary>Gets the statements removing the causation index and column.</summary>
    protected abstract IEnumerable<string> RemoveCausationSql { get; }

    /// <summary>Creates a connection to the provider's test database.</summary>
    protected abstract DbConnection CreateConnection();

    /// <summary>Gets the provider's current table DDL for the selected payload mode.</summary>
    /// <param name="binary">Whether the table stores message bodies as bytes.</param>
    protected abstract string CreateTableSql(bool binary);

    /// <summary>Creates an outbox using the isolated test table.</summary>
    /// <param name="configuration">The connection, table and payload configuration.</param>
    protected abstract RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration);

    /// <summary>Ensures the provider's test database exists before creating the table.</summary>
    protected virtual Task EnsureDatabaseAsync() => Task.CompletedTask;

    /// <summary>Releases provider-specific resources after dropping the test table.</summary>
    protected virtual Task CleanupDatabaseAsync() => Task.CompletedTask;

    /// <summary>
    /// Supplies the payload, I/O and write modes for tables supporting DataRef.
    /// </summary>
    public static IEnumerable<object[]> DataRefCases()
    {
        foreach (var binary in new[] { false, true })
        foreach (var useAsync in new[] { false, true })
        foreach (var bulk in new[] { false, true })
        foreach (var causation in new[] { false, true })
            yield return [binary, useAsync, bulk, causation];
    }

    /// <summary>
    /// Supplies the payload, I/O and write modes for an older table upgraded to support DataRef.
    /// </summary>
    public static IEnumerable<object[]> UpgradeCases()
    {
        foreach (var binary in new[] { false, true })
        foreach (var useAsync in new[] { false, true })
        foreach (var bulk in new[] { false, true })
            yield return [binary, useAsync, bulk];
    }

    /// <summary>
    /// A DataRef-only claim must survive the outbox without a legacy claim in the header bag.
    /// </summary>
    [Theory]
    [MemberData(nameof(DataRefCases))]
    public async Task When_relational_outbox_round_trips_dataref_should_retrieve_claimed_payload(
        bool binary, bool useAsync, bool bulk, bool causation)
    {
        // Arrange
        var outbox = await CreateStoreAsync(binary, causation);
        var storage = new InMemoryStorageProvider();
        var firstPayload = Encoding.UTF8.GetBytes("first claimed payload — café");
        var secondPayload = new byte[] { 0, 1, 127, 128, 255 };
        using var firstStream = new MemoryStream(firstPayload);
        using var secondStream = new MemoryStream(secondPayload);
        var firstRef = storage.Store(firstStream);
        var secondRef = storage.Store(secondStream);
        var messages = new[]
        {
            CreateMessage(firstRef, "first placeholder"),
            CreateMessage(null, "ordinary body"),
            CreateMessage(secondRef, "second placeholder"),
            CreateMessage(string.Empty, "empty reference body")
        };
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.CausationId] = "dataref-causation";
        var transformer = new ClaimCheckTransformer(storage, storage);
        transformer.InitializeUnwrapFromAttributeParams(true);

        // Act
        await WriteAsync(outbox, messages, context, useAsync, bulk);
        var retrieved = await ReadAsync(outbox, messages, context, useAsync);

        // Assert
        Assert.Equal(causation, useAsync
            ? await outbox.SupportsCausationTrackingAsync()
            : outbox.SupportsCausationTracking());
        for (var i = 0; i < messages.Length; i++)
        {
            Assert.Equal(messages[i].Header.DataRef, retrieved[i].Header.DataRef);
            Assert.Empty(retrieved[i].Header.Bag);
            Assert.Equal(messages[i].Body.ToByteArray(), retrieved[i].Body.ToByteArray());
        }

        var firstUnwrapped = useAsync
            ? await transformer.UnwrapAsync(retrieved[0])
            : transformer.Unwrap(retrieved[0]);
        var secondUnwrapped = useAsync
            ? await transformer.UnwrapAsync(retrieved[2])
            : transformer.Unwrap(retrieved[2]);
        Assert.Equal(firstPayload, firstUnwrapped.Body.ToByteArray());
        Assert.Equal(secondPayload, secondUnwrapped.Body.ToByteArray());
    }

    /// <summary>
    /// Ordinary writes remain usable before DataRef exists, and a new outbox uses the upgraded table.
    /// </summary>
    [Theory]
    [MemberData(nameof(UpgradeCases))]
    public async Task When_legacy_table_gains_dataref_should_preserve_old_messages_and_new_references(
        bool binary, bool useAsync, bool bulk)
    {
        // Arrange
        var legacyOutbox = await CreateStoreAsync(binary, causation: false);
        await ExecuteSqlAsync($"ALTER TABLE {TableName} DROP COLUMN DataRef");
        var oldMessages = new[] { CreateMessage(null, "old first"), CreateMessage(null, "old second") };
        var context = new RequestContext();
        await WriteAsync(legacyOutbox, oldMessages, context, useAsync, bulk);
        var oldRetrieved = await ReadAsync(legacyOutbox, oldMessages, context, useAsync);
        Assert.Equal("old first", oldRetrieved[0].Body.Value);
        Assert.Equal("old second", oldRetrieved[1].Body.Value);
        Assert.All(oldRetrieved, message => Assert.Null(message.Header.DataRef));
        await ExecuteSqlAsync(AddDataRefSql);
        var upgradedOutbox = CreateOutbox(_configuration!);
        var newMessages = new[]
        {
            CreateMessage("claim-after-upgrade-1", "new first"),
            CreateMessage("claim-after-upgrade-2", "new second")
        };

        // Act
        await WriteAsync(upgradedOutbox, newMessages, context, useAsync, bulk);
        var newRetrieved = await ReadAsync(upgradedOutbox, newMessages, context, useAsync);
        var oldAfterUpgrade = await ReadAsync(upgradedOutbox, oldMessages, context, useAsync);

        // Assert
        for (var i = 0; i < newMessages.Length; i++)
        {
            Assert.Equal(newMessages[i].Header.DataRef, newRetrieved[i].Header.DataRef);
            Assert.Empty(newRetrieved[i].Header.Bag);
            Assert.Equal(newMessages[i].Body.Value, newRetrieved[i].Body.Value);
            Assert.Null(oldAfterUpgrade[i].Header.DataRef);
            Assert.Equal(oldMessages[i].Body.Value, oldAfterUpgrade[i].Body.Value);
        }
    }

    /// <inheritdoc />
    public Task InitializeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        try
        {
            if (_tableCreated)
                await ExecuteSqlAsync($"DROP TABLE {TableName}");
        }
        finally
        {
            await CleanupDatabaseAsync();
        }
    }

    private async Task<RelationDatabaseOutbox> CreateStoreAsync(bool binary, bool causation)
    {
        await EnsureDatabaseAsync();
        _configuration = new RelationalDatabaseConfiguration(ConnectionString,
            outBoxTableName: TableName, binaryMessagePayload: binary);
        await ExecuteSqlAsync(CreateTableSql(binary));
        _tableCreated = true;
        if (!causation)
        {
            foreach (var sql in RemoveCausationSql)
                await ExecuteSqlAsync(sql);
        }
        return CreateOutbox(_configuration);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static Message CreateMessage(string? dataRef, string body) => new(
        new MessageHeader(Id.Random(), new RoutingKey("dataref-topic"), MessageType.MT_DOCUMENT)
        {
            DataRef = dataRef
        }, new MessageBody(body));

    private static async Task WriteAsync(RelationDatabaseOutbox outbox, Message[] messages,
        RequestContext context, bool useAsync, bool bulk)
    {
        if (bulk)
        {
            if (useAsync)
                await outbox.AddAsync(messages, context);
            else
                outbox.Add(messages, context);
            return;
        }

        foreach (var message in messages)
        {
            if (useAsync)
                await outbox.AddAsync(message, context);
            else
                outbox.Add(message, context);
        }
    }

    private static async Task<Message[]> ReadAsync(RelationDatabaseOutbox outbox, Message[] messages,
        RequestContext context, bool useAsync)
    {
        var retrieved = new Message[messages.Length];
        for (var i = 0; i < messages.Length; i++)
            retrieved[i] = useAsync
                ? await outbox.GetAsync(messages[i].Id, context)
                : outbox.Get(messages[i].Id, context);
        return retrieved;
    }
}
