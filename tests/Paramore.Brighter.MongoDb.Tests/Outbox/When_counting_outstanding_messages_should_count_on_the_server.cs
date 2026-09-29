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

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Property("Category", "MongoDB")]
public class OutstandingCountOnServerTests
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

    [Before(HookType.Test)]public async Task InitializeAsync()
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

    [Test]
    [Arguments(false, null, 100, 4)]
    [Arguments(true, null, 100, 4)]
    [Arguments(false, "selected", 100, 3)]
    [Arguments(true, "selected", 100, 3)]
    [Arguments(false, "selected", 2, 2)]
    [Arguments(true, "selected", 2, 2)]
    [Arguments(false, "selected", 0, 3)]
    [Arguments(true, "selected", 0, 3)]
    [Arguments(false, "missing", 100, 0)]
    [Arguments(true, "missing", 100, 0)]
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
        await Assert.That(count).IsEqualTo(expected);
        var command = await Assert.That(_commands.Values).HasSingleItem();
        await Assert.That(command.Contains("aggregate")).IsTrue();
        if (maxCount == 0)
            await Assert.That(System.Linq.Enumerable.Any(command["pipeline"].AsBsonArray, stage => stage.AsBsonDocument.Contains("$limit"))).IsFalse();
        else
            await Assert.That(System.Linq.Enumerable.Any(command["pipeline"].AsBsonArray, stage =>
                stage.AsBsonDocument.TryGetValue("$limit", out var limit) && limit.ToInt64() == maxCount)).IsTrue();

        var batch = (await Assert.That(_replies).HasSingleItem())["cursor"]["firstBatch"].AsBsonArray;
        if (expected == 0)
            await Assert.That(batch).IsEmpty();
        else
        {
            var result = (await Assert.That(batch).HasSingleItem()).AsBsonDocument;
            await Assert.That(result["n"].ToInt32()).IsEqualTo(expected);
            await Assert.That(result.Names.OrderBy(name => name)).IsEqualTo(new[] { "_id", "n" });
        }
    }

    private static OutboxMessage CreateMessage(string topic, DateTimeOffset timestamp, DateTimeOffset? dispatched = null)
    {
        var message = new Message(new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_DOCUMENT),
            new MessageBody(new string('x', 200_000)));
        return new OutboxMessage(message) { TimeStamp = timestamp, Dispatched = dispatched };
    }

    [After(HookType.Test)]public async Task DisposeAsync()
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
