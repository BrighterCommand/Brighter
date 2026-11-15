#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Outbox.DynamoDB.V4;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

[Property("Category", "DynamoDB")]
[NotInParallel("DynamoDBOutbox")]
public class DynamoDbTrippedTopicsTests
{
    private readonly string _tableName = $"brighter_tripped_topics_{Guid.NewGuid():N}";
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly RequestContext _context = new();

    [Test]
    [Arguments(false, 1)]
    [Arguments(true, 1)]
    [Arguments(false, 3)]
    [Arguments(true, 3)]
    public async Task When_retrieving_outstanding_messages_should_exclude_tripped_topics(bool isAsync, int scanConcurrency)
    {
        //Arrange
        var outbox = CreateOutbox(scanConcurrency);
        var orders = CreateMessage("orders");
        var payments = CreateMessage("payments");
        var quoted = CreateMessage("orders'archive");
        var differentCase = CreateMessage("Orders");
        var recent = CreateMessage("payments", TimeSpan.Zero);
        var dispatched = CreateMessage("payments");
        foreach (var message in new[] { orders, payments, quoted, differentCase, recent, dispatched })
        {
            await outbox.AddAsync(message, _context);
        }

        await outbox.MarkDispatchedAsync(dispatched.Id, _context);

        //Act
        var filtered = await ReadAsync(outbox, isAsync, [new RoutingKey("orders")]);

        //Assert
        await AssertIdsAsync(filtered, payments, quoted, differentCase);
        await AssertIdsAsync(await ReadAsync(outbox, isAsync, [new RoutingKey("orders"), new RoutingKey("orders'archive")]),
            payments, differentCase);
        await AssertIdsAsync(await ReadAsync(outbox, isAsync, [new RoutingKey("orders"), new RoutingKey("orders")]),
            payments, quoted, differentCase);
        await Assert.That(await ReadAsync(outbox, isAsync,
            [new RoutingKey("orders"), new RoutingKey("payments"), new RoutingKey("orders'archive"), new RoutingKey("Orders")],
            pageSize: 100)).IsEmpty();
        await AssertIdsAsync(await ReadAsync(outbox, isAsync, null), orders, payments, quoted, differentCase);
        await AssertIdsAsync(await ReadAsync(outbox, isAsync, []), orders, payments, quoted, differentCase);
        await AssertIdsAsync(await ReadAsync(outbox, isAsync, [new RoutingKey("unknown")]), orders, payments, quoted, differentCase);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_querying_a_tripped_topic_should_return_no_messages(bool isAsync)
    {
        //Arrange
        var outbox = CreateOutbox(1);
        var orders = CreateMessage("orders");
        await outbox.AddAsync(orders, _context);
        await outbox.AddAsync(CreateMessage("payments"), _context);
        var args = new Dictionary<string, object> { ["Topic"] = "orders" };

        //Act
        var filtered = await ReadAsync(outbox, isAsync, [new RoutingKey("orders")], args: args);

        //Assert
        await Assert.That(filtered).IsEmpty();
        await AssertIdsAsync(await ReadAsync(outbox, isAsync, [new RoutingKey("payments")], args: args), orders);
        await AssertIdsAsync(await ReadAsync(outbox, isAsync, [new RoutingKey("Orders")], args: args), orders);
        await AssertIdsAsync(await ReadAsync(outbox, isAsync, null, args: args), orders);
        await AssertIdsAsync(await ReadAsync(outbox, isAsync, [], args: args), orders);
    }

    [Test]
    [Arguments(false, 1)]
    [Arguments(true, 1)]
    [Arguments(false, 3)]
    [Arguments(true, 3)]
    public async Task When_paging_outstanding_messages_should_continue_after_partial_pages_without_tripped_topics(bool isAsync, int scanConcurrency)
    {
        //Arrange
        var outbox = CreateOutbox(scanConcurrency);
        var eligible = Enumerable.Range(0, 6).Select(_ => CreateMessage("payments")).ToArray();
        foreach (var message in Enumerable.Range(0, 12).Select(_ => CreateMessage("orders")).Concat(eligible))
        {
            await outbox.AddAsync(message, _context);
        }

        var received = new List<Message>();

        //Act
        for (var pageNumber = 1; pageNumber <= 18 && received.Count < eligible.Length; pageNumber++)
        {
            var page = await ReadAsync(outbox, isAsync, [new RoutingKey("orders")], pageSize: 3, pageNumber: pageNumber);
            await Assert.That(page.Length).IsGreaterThanOrEqualTo(0).And.IsLessThanOrEqualTo(3);

            received.AddRange(page);
        }

        //Assert
        await AssertIdsAsync(received, eligible);
        await Assert.That(received.Select(message => message.Id).Distinct().Count()).IsEqualTo(received.Count);
    }

    private DynamoDbOutbox CreateOutbox(int scanConcurrency)
        => new(Const.DynamoDbClient,
            new DynamoDbConfiguration(_tableName, timeout: 10000, scanConcurrency: scanConcurrency), _timeProvider);

    private Message CreateMessage(string topic, TimeSpan? age = null)
        => new(new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_EVENT,
                timeStamp: _timeProvider.GetUtcNow() - (age ?? TimeSpan.FromMinutes(10))),
            new MessageBody("body"));

    private async Task<Message[]> ReadAsync(DynamoDbOutbox outbox, bool isAsync, IEnumerable<RoutingKey>? trippedTopics,
        int pageSize = 100, int pageNumber = 1, Dictionary<string, object>? args = null)
    {
        var messages = isAsync
            ? await outbox.OutstandingMessagesAsync(TimeSpan.FromMinutes(1), _context, pageSize, pageNumber, trippedTopics, args)
            : outbox.OutstandingMessages(TimeSpan.FromMinutes(1), _context, pageSize, pageNumber, trippedTopics, args);
        return messages.ToArray();
    }

    private static async Task AssertIdsAsync(IEnumerable<Message> actual, params Message[] expected)
        => await Assert.That(actual.Select(message => message.Id.Value).OrderBy(id => id))
            .IsEquivalentTo(expected.Select(message => message.Id.Value).OrderBy(id => id),
                TUnit.Assertions.Enums.CollectionOrdering.Matching);

    [Before(HookType.Test)]
    public async Task InitializeAsync()
    {
        var request = new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                new Dictionary<string, ProvisionedThroughput>
                {
                    ["Outstanding"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["OutstandingAllTopics"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["Delivered"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["DeliveredAllTopics"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["Causation"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 }
                }));
        request.TableName = _tableName;
        var builder = new DynamoDbTableBuilder(Const.DynamoDbClient);
        await builder.Build(request);
        await builder.EnsureTablesReady([_tableName], TableStatus.ACTIVE);
    }

    [After(HookType.Test)]
    public async Task DisposeAsync() => await Const.DynamoDbClient.DeleteTableAsync(_tableName);
}
