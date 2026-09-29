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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Paramore.Brighter.Outbox.MongoDb;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Property("Category", "MongoDB")]
[NotInParallel("MongoDbOutbox")]
public class MongoDbTopicCountQueryTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_counting_by_topic_should_count_on_the_server_without_transferring_message_bodies(
        bool caseInsensitiveCollection)
    {
        //Arrange
        var collectionName = $"TopicCounts{Guid.NewGuid():N}";
        var commands = new ConcurrentQueue<(string Name, bool HasLimit)>();
        var countFields = new ConcurrentQueue<string[]>();
        var settings = MongoClientSettings.FromConnectionString(Const.ConnectionString);
        settings.ClusterConfigurator = builder =>
        {
            builder.Subscribe<CommandStartedEvent>(command => commands.Enqueue((command.CommandName,
                command.CommandName == "aggregate" && command.Command["pipeline"].AsBsonArray
                    .Any(stage => stage.AsBsonDocument.Contains("$limit")))));
            builder.Subscribe<CommandSucceededEvent>(reply =>
            {
                if (reply.CommandName != "aggregate")
                    return;
                foreach (var row in reply.Reply["cursor"]["firstBatch"].AsBsonArray)
                    countFields.Enqueue(row.AsBsonDocument.Names.ToArray());
            });
        };
        using var client = new MongoClient(settings);
        var now = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
        var outbox = new MongoDbOutbox(new MongoDbConfiguration(client, Const.DatabaseName)
        {
            TimeProvider = new FakeTimeProvider(now),
            Outbox = new MongoDbCollectionConfiguration { Name = collectionName }
        });
        var counting = (await Assert.That(outbox).IsAssignableTo<IAmATopicCountingOutbox>());
        var orders = new RoutingKey("orders");
        var payments = new RoutingKey("payments");
        var refunds = new RoutingKey("refunds");
        var ages = new Dictionary<RoutingKey, TimeSpan>
        {
            [orders] = TimeSpan.FromMinutes(5),
            [payments] = TimeSpan.FromMinutes(2),
            [refunds] = TimeSpan.FromMinutes(5)
        };
        var args = new Dictionary<string, object> { ["Topic"] = "unrequested", ["untouched"] = new object() };
        var originalArgs = args.ToArray();
        var body = new string('x', 256 * 1024);
        var database = client.GetDatabase(Const.DatabaseName);
        try
        {
            if (caseInsensitiveCollection)
                await database.CreateCollectionAsync(collectionName,
                    new CreateCollectionOptions { Collation = new Collation("en", strength: CollationStrength.Secondary) });
            foreach (var age in new[] { 8, 7, 6, 5, 4 })
                await outbox.AddAsync(CreateMessage(orders, now.AddMinutes(-age), body), null);
            foreach (var age in new[] { 3, 2, 1 })
                await outbox.AddAsync(CreateMessage(payments, now.AddMinutes(-age), body), null);
            await outbox.AddAsync(CreateMessage(new RoutingKey("Orders"), now.AddMinutes(-10), body), null);
            var dispatched = CreateMessage(orders, now.AddMinutes(-10), body);
            await outbox.AddAsync(dispatched, null);
            await outbox.MarkDispatchedAsync(dispatched.Id, new RequestContext());
            commands.Clear();
            countFields.Clear();

            //Act
            var capped = await counting.GetOutstandingMessageCountsByTopicAsync(ages, null, 2, args);
            var uncapped = await counting.GetOutstandingMessageCountsByTopicAsync(ages, null, 100, args);

            //Assert
            await Assert.That(capped.Count).IsEqualTo(3);
            await Assert.That(capped[orders]).IsEqualTo(2);
            await Assert.That(capped[payments]).IsEqualTo(1);
            await Assert.That(capped[refunds]).IsEqualTo(0);
            await Assert.That(uncapped.Count).IsEqualTo(3);
            await Assert.That(uncapped[orders]).IsEqualTo(3);
            await Assert.That(uncapped[payments]).IsEqualTo(1);
            await Assert.That(uncapped[refunds]).IsEqualTo(0);
            await Assert.That(args.ToArray()).IsEqualTo(originalArgs);
            await Assert.That(System.Linq.Enumerable.Any(commands, command => command.Name == "find")).IsFalse();
            var aggregates = commands.Where(command => command.Name == "aggregate").ToArray();
            await Assert.That(aggregates).IsNotEmpty();
            foreach (var command in aggregates)
{
    await Assert.That(command.HasLimit).IsTrue();
}
            await Assert.That(countFields).IsNotEmpty();
            foreach (var fields in countFields)
{
    await Assert.That(fields).Contains("n");
    foreach (var name in fields)
    {
        await Assert.That(name is "_id" or "n").IsTrue();
    }
}
        }
        finally
        {
            await database.DropCollectionAsync(collectionName);
        }
    }

    private static Message CreateMessage(RoutingKey topic, DateTimeOffset timestamp, string body) =>
        new(new MessageHeader(Id.Random(), topic, MessageType.MT_EVENT, timeStamp: timestamp), new MessageBody(body));
}
