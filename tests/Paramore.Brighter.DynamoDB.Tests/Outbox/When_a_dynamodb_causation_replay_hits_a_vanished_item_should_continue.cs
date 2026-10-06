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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

/// <summary>
/// Robustness (review finding #3): the Causation GSI is eventually consistent, so a replay query can
/// return a <c>MessageId</c> for an item that has since been swept / TTL-deleted / concurrently deleted.
/// The per-item conditional <c>UpdateItem</c> (<c>attribute_exists(MessageId)</c>) then fails with
/// <see cref="ConditionalCheckFailedException"/>. <c>ReplayCausationAsync</c> must catch-and-continue
/// (like <c>MarkDispatchedAsync</c>) so the remaining messages in the causation are still re-dispatched,
/// rather than let the exception unwind the pagination loop and silently skip them.
/// </summary>
[Trait("Category", "DynamoDB")]
public sealed class DynamoDbCausationReplayVanishedItemTests : IDisposable
{
    private const string CausationA = "causation-A";
    private readonly List<string> _tablesToDrop = [];

    [Fact]
    public async Task When_a_dynamodb_causation_replay_hits_a_vanished_item_should_continue()
    {
        // Arrange — two dispatched messages share causation A; both sit in the (eventually-consistent)
        // Causation GSI. We drive replay through a client that deletes the FIRST item the GSI returns
        // just before the caller updates it, reproducing the vanished-item race deterministically.
        var tableName = CreateOutboxTable();
        var seedOutbox = OutboxFor(Const.DynamoDbClient, tableName);

        var context = ContextWithCausation(CausationA);
        var firstWithA = CreateMessage();
        var secondWithA = CreateMessage();

        await seedOutbox.AddAsync(firstWithA, context);
        await seedOutbox.AddAsync(secondWithA, context);
        var dispatchedAt = DateTimeOffset.UtcNow;
        await seedOutbox.MarkDispatchedAsync(firstWithA.Id, context, dispatchedAt);
        await seedOutbox.MarkDispatchedAsync(secondWithA.Id, context, dispatchedAt);

        using var racingClient = new VanishingItemDynamoDbClient("Causation");
        var replayOutbox = OutboxFor(racingClient, tableName);

        // Act — replay; one item vanishes mid-loop and its conditional update fails
        var exception = await Xunit.Record.ExceptionAsync(() => replayOutbox.ReplayCausationAsync(CausationA, context));

        // Assert — the loop did not unwind: no exception, and the surviving message was re-dispatched
        Assert.Null(exception);
        Assert.NotNull(racingClient.DeletedMessageId);
        var survivorId = racingClient.DeletedMessageId == firstWithA.Id.Value ? secondWithA.Id : firstWithA.Id;
        var outstanding = (await seedOutbox.OutstandingMessagesAsync(TimeSpan.Zero, context)).Select(m => m.Id).ToArray();
        Assert.Contains(survivorId, outstanding);
    }

    /// <summary>
    /// A DynamoDB client that, on the first causation-index query returning more than one item, deletes
    /// the first returned item before handing the (now-stale) page back to the caller — emulating a GSI
    /// that still lists an item whose base record has been deleted.
    /// </summary>
    private sealed class VanishingItemDynamoDbClient : AmazonDynamoDBClient
    {
        private readonly string _causationIndexName;
        private bool _deleted;

        public string? DeletedMessageId { get; private set; }

        public VanishingItemDynamoDbClient(string causationIndexName)
            : base(
                new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey"),
                new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" })
        {
            _causationIndexName = causationIndexName;
        }

        public override async Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
        {
            var response = await base.QueryAsync(request, cancellationToken);

            if (!_deleted && request.IndexName == _causationIndexName && response.Items.Count > 1)
            {
                var victim = response.Items[0]["MessageId"].S;
                await base.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = request.TableName,
                    Key = new Dictionary<string, AttributeValue> { ["MessageId"] = new AttributeValue { S = victim } }
                }, cancellationToken);
                DeletedMessageId = victim;
                _deleted = true;
            }

            return response;
        }
    }

    private string CreateOutboxTable()
    {
        var tableName = $"brighter_outbox_replay_race_{Uuid.New():N}";
        var request = new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                new Dictionary<string, ProvisionedThroughput?>
                {
                    ["Outstanding"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["OutstandingAllTopics"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["Delivered"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["DeliveredAllTopics"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["Causation"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                }));

        request.TableName = tableName;

        var builder = new DynamoDbTableBuilder(Const.DynamoDbClient);
        builder.Build(request).GetAwaiter().GetResult();
        builder.EnsureTablesReady([tableName], Amazon.DynamoDBv2.TableStatus.ACTIVE).GetAwaiter().GetResult();
        _tablesToDrop.Add(tableName);
        return tableName;
    }

    private static DynamoDbOutbox OutboxFor(IAmazonDynamoDB client, string tableName)
        => new(client, new DynamoDbConfiguration { TableName = tableName });

    private static Message CreateMessage()
        => new(
            new MessageHeader(Id.Random(), new RoutingKey("causation_topic"), MessageType.MT_DOCUMENT),
            new MessageBody("message body"));

    private static RequestContext ContextWithCausation(string causationId)
    {
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.CausationId] = causationId;
        return context;
    }

    public void Dispose()
    {
        foreach (var table in _tablesToDrop)
        {
            try
            {
                Const.DynamoDbClient.DeleteTableAsync(table).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // best-effort cleanup
            }
        }
    }
}
