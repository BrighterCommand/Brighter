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
using System.Threading.Tasks;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusContextPartitionKeyTests
{
    private readonly FakeServiceBusSenderWrapper _sender = new();
    private readonly AzureServiceBusPublication _publication = new()
    {
        Topic = new RoutingKey("orders"),
        MakeChannels = OnMissingChannel.Assume
    };

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task When_sending_a_message_should_preserve_the_context_partition_key(
        bool useQueue, bool useAsync, bool scheduled)
    {
        // Arrange
        using var producer = CreateProducer(useQueue);
        var message = CreateMessage();
        var delay = TimeSpan.FromMinutes(1);

        // Act
        if (useAsync)
        {
            if (scheduled)
                await producer.SendWithDelayAsync(message, delay);
            else
                await producer.SendAsync(message);
        }
        else
        {
            if (scheduled)
                producer.SendWithDelay(message, delay);
            else
                producer.Send(message);
        }

        // Assert
        var sent = Assert.Single(_sender.SentMessages);
        Assert.Equal("101", sent.PartitionKey);
        Assert.Equal("101", sent.ApplicationProperties["cloudEvents:partitionkey"]);
        Assert.Equal(message.Id.Value, sent.MessageId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_sending_batches_should_preserve_the_context_partition_key(
        bool useQueue, bool oversized)
    {
        // Arrange
        using var producer = CreateProducer(useQueue);
        Message[] messages = [CreateMessage(), CreateMessage()];
        _sender.TryAddMessageCallBack = message =>
        {
            Assert.Equal("101", message.PartitionKey);
            return !oversized;
        };

        // Act
        var batches = await producer.CreateBatchesAsync(messages, default);
        foreach (var batch in batches)
            await producer.SendAsync(batch, default);

        // Assert
        if (oversized)
            Assert.All(batches, batch => Assert.IsType<AzureServiceBusSingleMessageBatch>(batch));
        else
            Assert.IsType<AzureServiceBusMessageBatch>(Assert.Single(batches));
        Assert.Equal(messages.Length, _sender.SentMessages.Count);
        foreach (var message in messages)
        {
            var sent = Assert.Single(_sender.SentMessages, sent => sent.MessageId == message.Id.Value);
            Assert.Equal("101", sent.PartitionKey);
            Assert.Equal("101", sent.ApplicationProperties["cloudEvents:partitionkey"]);
        }
    }

    private AzureServiceBusMessageProducer CreateProducer(bool useQueue)
    {
        var administrationClient = new FakeAdministrationClient();
        var senderProvider = new FakeServiceBusSenderProvider(_sender);
        return useQueue
            ? new AzureServiceBusQueueMessageProducer(administrationClient, senderProvider, _publication)
            : new AzureServiceBusTopicMessageProducer(administrationClient, senderProvider, _publication);
    }

    private Message CreateMessage()
    {
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.PartitionKey] = new PartitionKey("101");
        var mapper = new JsonMessageMapper<ASBTestEvent> { Context = context };
        return mapper.MapToMessage(new ASBTestEvent { EventName = "order-placed" }, _publication);
    }
}
