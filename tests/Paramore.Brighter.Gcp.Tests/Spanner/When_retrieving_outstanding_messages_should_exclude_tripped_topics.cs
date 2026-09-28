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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Outbox.Spanner;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Spanner;

[Trait("Category", "Spanner")]
[Collection("SpannerBoxProvisioning")]
public class SpannerOutstandingMessagesTrippedTopicsTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_retrieving_outstanding_messages_should_exclude_tripped_topics(bool binaryPayload, bool isAsync)
    {
        //Arrange
        var configuration = new RelationalDatabaseConfiguration(Const.ConnectionString,
            outBoxTableName: $"test_{Guid.NewGuid():N}", binaryMessagePayload: binaryPayload);
        using var connection = new SpannerConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        using var create = connection.CreateDdlCommand(SpannerOutboxBuilder.GetDDL(configuration.OutBoxTableName, binaryPayload));
        await create.ExecuteNonQueryAsync();

        try
        {
            var outbox = new SpannerOutbox(configuration);
            var context = new RequestContext();
            var timestamp = DateTimeOffset.UtcNow.AddHours(-2);
            var orders = CreateMessage("orders", timestamp.AddMinutes(3));
            var payments = CreateMessage("payments", timestamp.AddMinutes(2));
            var shipments = CreateMessage("shipments'priority", timestamp.AddMinutes(1));
            var dispatched = CreateMessage("payments", timestamp);
            var recent = CreateMessage("payments", DateTimeOffset.UtcNow);
            await outbox.AddAsync([orders, payments, shipments, dispatched, recent], context);
            await outbox.MarkDispatchedAsync(dispatched.Id, context);

            //Act
            var messages = await ReadOutstandingAsync([orders.Header.Topic]);

            //Assert
            Assert.Equal(new[] { payments.Id, shipments.Id }, messages.Select(message => message.Id));
            Assert.Equal(new[] { orders.Id, payments.Id, shipments.Id },
                (await ReadOutstandingAsync(null)).Select(message => message.Id));
            Assert.Equal(new[] { orders.Id, payments.Id, shipments.Id },
                (await ReadOutstandingAsync([])).Select(message => message.Id));
            Assert.Equal(new[] { orders.Id, payments.Id, shipments.Id },
                (await ReadOutstandingAsync([new RoutingKey("unknown")])).Select(message => message.Id));
            Assert.Equal(new[] { payments.Id },
                (await ReadOutstandingAsync([orders.Header.Topic, shipments.Header.Topic])).Select(message => message.Id));
            Assert.Empty(await ReadOutstandingAsync([orders.Header.Topic, payments.Header.Topic, shipments.Header.Topic]));

            Assert.Equal(payments.Id, Assert.Single(await ReadOutstandingAsync([orders.Header.Topic], 1, 1)).Id);
            Assert.Equal(shipments.Id, Assert.Single(await ReadOutstandingAsync([orders.Header.Topic], 1, 2)).Id);
            Assert.Empty(await ReadOutstandingAsync([orders.Header.Topic], 1, 3));
            Assert.Equal(new[] { orders.Id, payments.Id, shipments.Id },
                (await ReadOutstandingAsync(null)).Select(message => message.Id));

            async Task<Message[]> ReadOutstandingAsync(IEnumerable<RoutingKey>? trippedTopics, int pageSize = 100, int pageNumber = 1)
            {
                var outstanding = isAsync
                    ? await outbox.OutstandingMessagesAsync(TimeSpan.FromHours(1), context, pageSize, pageNumber, trippedTopics)
                    : outbox.OutstandingMessages(TimeSpan.FromHours(1), context, pageSize, pageNumber, trippedTopics);
                return outstanding.ToArray();
            }
        }
        finally
        {
            using var drop = connection.CreateDdlCommand($"DROP TABLE `{configuration.OutBoxTableName}`");
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static Message CreateMessage(string topic, DateTimeOffset timestamp) =>
        new(new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_EVENT, timeStamp: timestamp),
            new MessageBody("payload"));
}
