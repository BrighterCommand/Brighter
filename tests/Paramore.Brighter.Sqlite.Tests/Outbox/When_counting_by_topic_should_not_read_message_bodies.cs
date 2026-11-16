#region Licence

/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Trait("Category", "Sqlite")]
public class TopicCountWithoutMessageBodiesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteOutbox _outbox;
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _payments = new("payments");
    private readonly RoutingKey _refunds = new("refunds");

    public TopicCountWithoutMessageBodiesTests()
    {
        var connectionString = $"Data Source={Uuid.New():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE messages (Topic TEXT NOT NULL, Timestamp TEXT NOT NULL, Dispatched TEXT NULL);
            INSERT INTO messages VALUES
                ('orders', @Old, NULL), ('orders', @Old, NULL), ('orders', @Old, NULL),
                ('orders', @ThreeMinutes, NULL), ('orders', @Recent, NULL), ('orders', @Old, @Recent), ('Orders', @Old, NULL),
                ('payments', @ThreeMinutes, NULL), ('audit', @Old, NULL), ('audit', @Old, NULL);
            CREATE VIEW outbox AS
                SELECT Topic, Timestamp, Dispatched, json('unreadable body') AS Body FROM messages;
            """;
        command.Parameters.AddWithValue("@Old", DateTimeOffset.UtcNow.AddMinutes(-10));
        command.Parameters.AddWithValue("@ThreeMinutes", DateTimeOffset.UtcNow.AddMinutes(-3));
        command.Parameters.AddWithValue("@Recent", DateTimeOffset.UtcNow.AddMinutes(-1));
        command.ExecuteNonQuery();
        _outbox = new SqliteOutbox(new RelationalDatabaseConfiguration(connectionString, outBoxTableName: "outbox"));
    }

    [Theory]
    [InlineData(100, 3)]
    [InlineData(2, 2)]
    public async Task When_counting_by_topic_should_not_read_message_bodies(
        int maxCountPerTopic, int expectedOrders)
    {
        //Arrange
        var counting = Assert.IsAssignableFrom<IAmATopicCountingOutbox>(_outbox);
        var ages = new Dictionary<RoutingKey, TimeSpan>
        {
            [_orders] = TimeSpan.FromMinutes(5),
            [_payments] = TimeSpan.FromMinutes(2),
            [_refunds] = TimeSpan.FromMinutes(5)
        };

        //Act
        var counts = await counting.GetOutstandingMessageCountsByTopicAsync(ages, new RequestContext(), maxCountPerTopic);

        //Assert
        IReadOnlyDictionary<RoutingKey, int> expected = new Dictionary<RoutingKey, int>
        {
            [_orders] = expectedOrders,
            [_payments] = 1,
            [_refunds] = 0
        };
        Assert.Equal(expected, counts);
    }

    public void Dispose() => _connection.Dispose();
}
