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

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.MSSQL.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

[Trait("Category", "MSSQL")]
public class MsSqlTopicCountExactTopicTests
{
    [Fact]
    public async Task When_mssql_counts_by_topic_should_match_the_topic_exactly()
    {
        //Arrange
        await using var store = new MsSqlMessageIdOutboxStore(binary: false);
        await store.InitializeAsync();
        var outbox = new MsSqlOutbox(store.OutboxConfig);
        var context = new RequestContext();
        await outbox.AddAsync(StaleMessage("orders"), context);
        await outbox.AddAsync(StaleMessage("orders"), context);
        await outbox.AddAsync(StaleMessage("Orders"), context);
        var counting = Assert.IsAssignableFrom<IAmATopicCountingOutbox>(outbox);
        var orders = new RoutingKey("orders");
        var ages = new Dictionary<RoutingKey, TimeSpan> { [orders] = TimeSpan.FromMinutes(5) };

        //Act
        var counts = await counting.GetOutstandingMessageCountsByTopicAsync(ages, context, 100);

        //Assert
        IReadOnlyDictionary<RoutingKey, int> expected = new Dictionary<RoutingKey, int> { [orders] = 2 };
        Assert.Equal(expected, counts);
    }

    private static Message StaleMessage(string topic) => new(
        new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_COMMAND,
            timeStamp: DateTimeOffset.UtcNow.AddMinutes(-10)), new MessageBody("payload"));
}
