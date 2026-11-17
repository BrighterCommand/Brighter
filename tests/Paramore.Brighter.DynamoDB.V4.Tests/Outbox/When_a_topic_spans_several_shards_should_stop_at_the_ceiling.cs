#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
using System.Threading.Tasks;
using Paramore.Brighter.Outbox.DynamoDB.V4;
using Xunit;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

[Trait("Category", "DynamoDB")]
[Collection("DynamoDBOutbox")]
public class DynamoDbShardCeilingTopicCountTests : IAsyncLifetime
{
    private const int NUMBER_OF_SHARDS = 3;
    private const string PARTITION_KEY_ON_SHARD_ZERO = "partition-1";
    private const string PARTITION_KEY_ON_SHARD_MINUS_ONE = "partition-2";
    private const string PARTITION_KEY_ON_SHARD_MINUS_TWO = "partition-19";
    private const int CEILING = 2;

    private readonly RoutingKey _orders = new($"orders-{Uuid.New():N}");
    private readonly RequestContext _context = new();
    private string _tableName = "";

    [Fact]
    public async Task When_a_topic_spans_several_shards_should_stop_at_the_ceiling()
    {
        //Arrange
        var outbox = new DynamoDbOutbox(Const.DynamoDbClient,
            new DynamoDbConfiguration(_tableName, timeout: Const.OUTBOX_TIMEOUT_IN_MILLISECONDS, numberOfShards: NUMBER_OF_SHARDS));
        await outbox.AddAsync(CreateStaleMessage(PARTITION_KEY_ON_SHARD_ZERO), _context);
        await outbox.AddAsync(CreateStaleMessage(PARTITION_KEY_ON_SHARD_MINUS_ONE), _context);
        await outbox.AddAsync(CreateStaleMessage(PARTITION_KEY_ON_SHARD_MINUS_ONE), _context);
        await outbox.AddAsync(CreateStaleMessage(PARTITION_KEY_ON_SHARD_MINUS_TWO), _context);

        //Act
        var counts = await outbox.GetOutstandingMessageCountsByTopicAsync(
            new Dictionary<RoutingKey, TimeSpan> { [_orders] = TimeSpan.FromMinutes(5) }, _context, CEILING);

        //Assert
        Assert.Equal(CEILING, counts[_orders]);
    }

    private Message CreateStaleMessage(string partitionKey)
        => new(new MessageHeader(Id.Random(), _orders, MessageType.MT_EVENT,
                timeStamp: DateTimeOffset.UtcNow - TimeSpan.FromMinutes(10), partitionKey: partitionKey),
            new MessageBody("body"));

    public async Task InitializeAsync() => _tableName = await DynamoDbOutboxTable.CreateTableAsync(Const.DynamoDbClient);

    public async Task DisposeAsync() => await DynamoDbOutboxTable.DeleteTableAsync(Const.DynamoDbClient, _tableName);
}
