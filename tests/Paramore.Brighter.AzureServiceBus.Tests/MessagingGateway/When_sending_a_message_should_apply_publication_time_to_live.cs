#region Licence

/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia <irakli.gabisonia94@gmail.com>

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
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusPublicationTimeToLiveTests
{
    private readonly FakeServiceBusSenderWrapper _sender = new();
    private readonly AzureServiceBusPublication _publication = new AzureServiceBusPublication<ASBTestEvent>
    {
        MakeChannels = OnMissingChannel.Assume
    };

    public static IEnumerable<object?[]> SendCases()
    {
        foreach (var useQueues in new[] { false, true })
        {
            foreach (var useAsync in new[] { false, true })
            {
                foreach (var scheduled in new[] { false, true })
                {
                    foreach (var timeToLive in new TimeSpan?[] { null, TimeSpan.FromMinutes(5), TimeSpan.MaxValue })
                        yield return [useQueues, useAsync, scheduled, timeToLive];
                }
            }
        }
    }

    public static IEnumerable<object?[]> BatchCases()
    {
        foreach (var useQueues in new[] { false, true })
        {
            foreach (var batchCapacity in new[] { 0, 1, 2 })
            {
                foreach (var timeToLive in new TimeSpan?[] { null, TimeSpan.FromMinutes(5), TimeSpan.MaxValue })
                    yield return [useQueues, batchCapacity, timeToLive];
            }
        }
    }

    [Theory]
    [MemberData(nameof(SendCases))]
    public async Task When_sending_a_message_should_apply_publication_time_to_live(
        bool useQueues, bool useAsync, bool scheduled, TimeSpan? timeToLive)
    {
        // Arrange
        _publication.TimeToLive = timeToLive;
        using var producer = CreateProducer(useQueues);
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
        AssertMessage(Assert.Single(_sender.SentMessages), message, timeToLive);
        if (scheduled)
            Assert.Same(_sender.SentMessages[0], Assert.Single(_sender.ScheduledMessages));
        else
            Assert.Empty(_sender.ScheduledMessages);
    }

    [Theory]
    [MemberData(nameof(BatchCases))]
    public async Task When_sending_batches_should_apply_publication_time_to_live_to_every_message(
        bool useQueues, int batchCapacity, TimeSpan? timeToLive)
    {
        // Arrange
        _publication.TimeToLive = timeToLive;
        using var producer = CreateProducer(useQueues);
        Message[] messages = [CreateMessage(), CreateMessage()];
        var attempts = 0;
        _sender.TryAddMessageCallBack = message =>
        {
            Assert.Equal(timeToLive, message.GetRawAmqpMessage().Header.TimeToLive);
            attempts++;
            return batchCapacity > 0 && attempts % (batchCapacity + 1) != 0;
        };

        // Act
        var batches = (await producer.CreateBatchesAsync(messages, default)).ToArray();
        foreach (var batch in batches)
            await producer.SendAsync(batch, default);

        // Assert
        Assert.Equal(batchCapacity == 2 ? 1 : 2, batches.Length);
        Assert.Equal(messages.Length, _sender.SentMessages.Count);
        if (batchCapacity == 0)
            Assert.All(batches, batch => Assert.IsType<AzureServiceBusSingleMessageBatch>(batch));
        else
            Assert.All(batches, batch => Assert.IsType<AzureServiceBusMessageBatch>(batch));
        foreach (var message in messages)
        {
            var sent = Assert.Single(_sender.SentMessages, sent => sent.MessageId == message.Id.Value);
            AssertMessage(sent, message, timeToLive);
        }
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, 300)]
    [InlineData(true, 300)]
    public async Task When_a_batch_contains_an_oversized_message_should_preserve_time_to_live_for_all_messages(
        bool useQueues, int? seconds)
    {
        // Arrange
        TimeSpan? timeToLive = seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value) : null;
        _publication.TimeToLive = timeToLive;
        using var producer = CreateProducer(useQueues);
        var firstMessage = CreateMessage();
        var oversizedMessage = CreateMessage();
        var lastMessage = CreateMessage();
        Message[] messages = [firstMessage, oversizedMessage, lastMessage];
        _sender.TryAddMessageCallBack = message =>
        {
            Assert.Equal(timeToLive, message.GetRawAmqpMessage().Header.TimeToLive);
            return message.MessageId != oversizedMessage.Id.Value;
        };

        // Act
        var batches = (await producer.CreateBatchesAsync(messages, default)).ToArray();
        foreach (var batch in batches)
            await producer.SendAsync(batch, default);

        // Assert
        Assert.Equal(2, batches.OfType<AzureServiceBusMessageBatch>().Count());
        Assert.Single(batches.OfType<AzureServiceBusSingleMessageBatch>());
        Assert.Equal(messages.Length, _sender.SentMessages.Count);
        foreach (var message in messages)
        {
            var sent = Assert.Single(_sender.SentMessages, sent => sent.MessageId == message.Id.Value);
            AssertMessage(sent, message, timeToLive);
        }
    }

    [Fact]
    public void When_publication_time_to_live_is_not_configured_should_use_entity_default()
    {
        // Arrange
        var publication = new AzureServiceBusPublication();

        // Act
        var timeToLive = publication.TimeToLive;

        // Assert
        Assert.Null(timeToLive);
        Assert.Null(_publication.TimeToLive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void When_publication_time_to_live_is_not_positive_should_reject_value(long ticks)
    {
        // Arrange
        var originalTimeToLive = TimeSpan.FromMinutes(5);
        _publication.TimeToLive = originalTimeToLive;

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => _publication.TimeToLive = TimeSpan.FromTicks(ticks));

        // Assert
        Assert.Equal("value", exception.ParamName);
        Assert.Equal(originalTimeToLive, _publication.TimeToLive);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void When_publication_time_to_live_is_positive_should_accept_value(long ticks)
    {
        // Arrange
        var timeToLive = TimeSpan.FromTicks(ticks);

        // Act
        _publication.TimeToLive = timeToLive;

        // Assert
        Assert.Equal(timeToLive, _publication.TimeToLive);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task When_clearing_publication_time_to_live_should_restore_entity_default(bool useQueues, bool bulk)
    {
        // Arrange
        var timeToLive = TimeSpan.FromMinutes(5);
        _publication.TimeToLive = timeToLive;
        using var producer = CreateProducer(useQueues);
        var firstMessage = CreateMessage();
        var secondMessage = CreateMessage();
        await SendMessageAsync(firstMessage);

        // Act
        _publication.TimeToLive = null;
        await SendMessageAsync(secondMessage);

        // Assert
        Assert.Collection(_sender.SentMessages,
            sent => AssertMessage(sent, firstMessage, timeToLive),
            sent => AssertMessage(sent, secondMessage, null));

        async Task SendMessageAsync(Message message)
        {
            if (bulk)
            {
                var batches = await producer.CreateBatchesAsync([message], default);
                foreach (var batch in batches)
                    await producer.SendAsync(batch, default);
            }
            else
            {
                await producer.SendAsync(message);
            }
        }
    }

    private AzureServiceBusMessageProducer CreateProducer(bool useQueues)
    {
        var administrationClient = new FakeAdministrationClient();
        var senderProvider = new FakeServiceBusSenderProvider(_sender);
        return useQueues
            ? new AzureServiceBusQueueMessageProducer(administrationClient, senderProvider, _publication)
            : new AzureServiceBusTopicMessageProducer(administrationClient, senderProvider, _publication);
    }

    private static Message CreateMessage() => new(
        new MessageHeader(Id.Random(), new RoutingKey("ttl-topic"), MessageType.MT_EVENT),
        new MessageBody("A message body"));

    private static void AssertMessage(ServiceBusMessage sent, Message original, TimeSpan? timeToLive)
    {
        Assert.Equal(timeToLive, sent.GetRawAmqpMessage().Header.TimeToLive);
        Assert.Equal(timeToLive ?? TimeSpan.MaxValue, sent.TimeToLive);
        Assert.Equal(original.Id.Value, sent.MessageId);
        Assert.Equal(original.Body.Value, sent.Body.ToString());
    }
}
