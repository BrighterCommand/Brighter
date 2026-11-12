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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDB")]
public class OutstandingCountOnServerTests : IAsyncLifetime
{
    private readonly MongoClient _client;
    private readonly string _collectionName = $"Outbox{Uuid.New():N}";
    private readonly MongoDbOutbox _outbox;
    private readonly ConcurrentDictionary<int, BsonDocument> _commands = new();
    private readonly ConcurrentQueue<BsonDocument> _replies = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

    public OutstandingCountOnServerTests()
    {
        var settings = MongoClientSettings.FromConnectionString(Const.ConnectionString);
        settings.ClusterConfigurator = builder =>
        {
            builder.Subscribe<CommandStartedEvent>(e =>
            {
                if ((e.CommandName == "find" || e.CommandName == "aggregate") &&
                    e.Command[e.CommandName] == _collectionName)
                    _commands.TryAdd(e.RequestId, BsonSerializer.Deserialize<BsonDocument>(e.Command.ToBson()));
            });
            builder.Subscribe<CommandSucceededEvent>(e =>
            {
                if (_commands.ContainsKey(e.RequestId))
                    _replies.Enqueue(BsonSerializer.Deserialize<BsonDocument>(e.Reply.ToBson()));
            });
        };
        _client = new MongoClient(settings);
        _outbox = new MongoDbOutbox(new MongoDbConfiguration(_client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration { Name = _collectionName },
            TimeProvider = _clock
        });
    }

    public async Task InitializeAsync()
    {
        var old = _clock.GetUtcNow().AddMinutes(-10);
        var recent = _clock.GetUtcNow().AddMinutes(-1);
        var collection = _client.GetDatabase(Const.DatabaseName).GetCollection<OutboxMessage>(_collectionName);
        await collection.InsertManyAsync([
            CreateMessage("selected", old), CreateMessage("selected", old), CreateMessage("selected", old),
            CreateMessage("other", old), CreateMessage("selected", recent),
            CreateMessage("selected", old, recent)
        ]);
    }

    [Theory]
    [InlineData(false, null, 100, 4)]
    [InlineData(true, null, 100, 4)]
    [InlineData(false, "selected", 100, 3)]
    [InlineData(true, "selected", 100, 3)]
    [InlineData(false, "selected", 2, 2)]
    [InlineData(true, "selected", 2, 2)]
    [InlineData(false, "selected", 0, 3)]
    [InlineData(true, "selected", 0, 3)]
    [InlineData(false, "missing", 100, 0)]
    [InlineData(true, "missing", 100, 0)]
    public async Task When_counting_outstanding_messages_should_count_on_the_server(bool useAsync, string? topic, int maxCount, int expected)
    {
        //Arrange
        var args = topic == null ? null : new Dictionary<string, object> { ["Topic"] = topic };
        var age = TimeSpan.FromMinutes(5);

        //Act
        var count = useAsync
            ? await _outbox.GetOutstandingMessageCountAsync(age, new RequestContext(), maxCount, args)
            : _outbox.GetOutstandingMessageCount(age, new RequestContext(), maxCount, args);

        //Assert
        Assert.Equal(expected, count);
        var command = Assert.Single(_commands.Values);
        Assert.True(command.Contains("aggregate"));
        if (maxCount == 0)
            Assert.DoesNotContain(command["pipeline"].AsBsonArray, stage => stage.AsBsonDocument.Contains("$limit"));
        else
            Assert.Contains(command["pipeline"].AsBsonArray, stage =>
                stage.AsBsonDocument.TryGetValue("$limit", out var limit) && limit.ToInt64() == maxCount);

        var batch = Assert.Single(_replies)["cursor"]["firstBatch"].AsBsonArray;
        if (expected == 0)
            Assert.Empty(batch);
        else
        {
            var result = Assert.Single(batch).AsBsonDocument;
            Assert.Equal(expected, result["n"].ToInt32());
            Assert.Equal(new[] { "_id", "n" }, result.Names.OrderBy(name => name));
        }
    }

    private static OutboxMessage CreateMessage(string topic, DateTimeOffset timestamp, DateTimeOffset? dispatched = null)
    {
        var message = new Message(new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_DOCUMENT),
            new MessageBody(new string('x', 200_000)));
        return new OutboxMessage(message) { TimeStamp = timestamp, Dispatched = dispatched };
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _client.GetDatabase(Const.DatabaseName).DropCollectionAsync(_collectionName);
        }
        finally
        {
            var cluster = _client.Cluster;
            _client.Dispose();
            ClusterRegistry.Instance.UnregisterAndDisposeCluster(cluster);
        }
    }
}
