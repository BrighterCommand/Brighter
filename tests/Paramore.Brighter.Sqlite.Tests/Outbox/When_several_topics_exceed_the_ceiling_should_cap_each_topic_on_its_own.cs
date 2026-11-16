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
public class TopicCountCeilingPerTopicTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IAmATopicCountingOutbox _outbox;
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _payments = new("payments");
    private readonly RoutingKey _refunds = new("refunds");

    public TopicCountCeilingPerTopicTests()
    {
        var connectionString = $"Data Source={Uuid.New():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE outbox (Topic TEXT NOT NULL, Timestamp TEXT NOT NULL, Dispatched TEXT NULL);
            INSERT INTO outbox VALUES
                ('payments', @Old, NULL), ('orders', @Old, NULL), ('payments', @Old, NULL), ('orders', @Old, NULL),
                ('payments', @Old, NULL), ('orders', @Old, NULL), ('payments', @Old, NULL), ('orders', @Old, NULL),
                ('refunds', @Old, NULL);
            """;
        command.Parameters.AddWithValue("@Old", DateTimeOffset.UtcNow.AddMinutes(-10));
        command.ExecuteNonQuery();
        _outbox = new SqliteOutbox(new RelationalDatabaseConfiguration(connectionString, outBoxTableName: "outbox"));
    }

    [Fact]
    public async Task When_several_topics_exceed_the_ceiling_should_cap_each_topic_on_its_own()
    {
        //Arrange
        var ages = new Dictionary<RoutingKey, TimeSpan>
        {
            [_orders] = TimeSpan.FromMinutes(5),
            [_payments] = TimeSpan.FromMinutes(5),
            [_refunds] = TimeSpan.FromMinutes(5)
        };

        //Act
        var counts = await _outbox.GetOutstandingMessageCountsByTopicAsync(ages, new RequestContext(), 2);

        //Assert
        IReadOnlyDictionary<RoutingKey, int> expected = new Dictionary<RoutingKey, int>
        {
            [_orders] = 2,
            [_payments] = 2,
            [_refunds] = 1
        };
        Assert.Equal(expected, counts);
    }

    public void Dispose() => _connection.Dispose();
}
