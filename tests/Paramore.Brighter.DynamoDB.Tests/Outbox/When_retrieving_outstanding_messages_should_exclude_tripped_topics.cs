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
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

[Trait("Category", "DynamoDB")]
[Collection("DynamoDBOutbox")]
public class DynamoDbTrippedTopicsTests : IAsyncLifetime
{
    private readonly string _tableName = $"brighter_tripped_topics_{Guid.NewGuid():N}";
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly RequestContext _context = new();

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
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
        AssertIds(filtered, payments, quoted, differentCase);
        AssertIds(await ReadAsync(outbox, isAsync, [new RoutingKey("orders"), new RoutingKey("orders'archive")]),
            payments, differentCase);
        AssertIds(await ReadAsync(outbox, isAsync, [new RoutingKey("orders"), new RoutingKey("orders")]),
            payments, quoted, differentCase);
        Assert.Empty(await ReadAsync(outbox, isAsync,
            [new RoutingKey("orders"), new RoutingKey("payments"), new RoutingKey("orders'archive"), new RoutingKey("Orders")],
            pageSize: 3));
        AssertIds(await ReadAsync(outbox, isAsync, null), orders, payments, quoted, differentCase);
        AssertIds(await ReadAsync(outbox, isAsync, []), orders, payments, quoted, differentCase);
        AssertIds(await ReadAsync(outbox, isAsync, [new RoutingKey("unknown")]), orders, payments, quoted, differentCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
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
        Assert.Empty(filtered);
        AssertIds(await ReadAsync(outbox, isAsync, [new RoutingKey("payments")], args: args), orders);
        AssertIds(await ReadAsync(outbox, isAsync, [new RoutingKey("Orders")], args: args), orders);
        AssertIds(await ReadAsync(outbox, isAsync, null, args: args), orders);
        AssertIds(await ReadAsync(outbox, isAsync, [], args: args), orders);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public async Task When_paging_outstanding_messages_should_fill_pages_without_tripped_topics(bool isAsync, int scanConcurrency)
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
        for (var pageNumber = 1; pageNumber <= eligible.Length && received.Count < eligible.Length; pageNumber++)
        {
            var page = await ReadAsync(outbox, isAsync, [new RoutingKey("orders")], pageSize: 3, pageNumber: pageNumber);
            Assert.NotEmpty(page);
            Assert.InRange(page.Length, 1, 3);
            if (scanConcurrency == 1)
            {
                Assert.Equal(3, page.Length);
            }

            received.AddRange(page);
        }

        //Assert
        AssertIds(received, eligible);
        Assert.Equal(received.Count, received.Select(message => message.Id).Distinct().Count());
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

    private static void AssertIds(IEnumerable<Message> actual, params Message[] expected)
        => Assert.Equal(expected.Select(message => message.Id.Value).OrderBy(id => id),
            actual.Select(message => message.Id.Value).OrderBy(id => id));

    public async Task InitializeAsync()
    {
        var request = new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                new Dictionary<string, ProvisionedThroughput?>
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

    public async Task DisposeAsync() => await Const.DynamoDbClient.DeleteTableAsync(_tableName);
}
