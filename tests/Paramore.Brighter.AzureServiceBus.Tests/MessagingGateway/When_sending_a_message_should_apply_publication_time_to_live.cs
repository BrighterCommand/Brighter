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

    [Test]
    [MethodDataSource(nameof(SendCases))]
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
        await AssertMessage((await Assert.That(_sender.SentMessages).HasSingleItem()), message, timeToLive);
        if (scheduled)
            await Assert.That((await Assert.That(_sender.ScheduledMessages).HasSingleItem())).IsSameReferenceAs(_sender.SentMessages[0]);
        else
            await Assert.That(_sender.ScheduledMessages).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(BatchCases))]
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
#pragma warning disable TUnitAssertions0002 // Synchronous callback: the assertion is executed by GetResult.
            Assert.That(message.GetRawAmqpMessage().Header.TimeToLive).IsEqualTo(timeToLive).GetAwaiter().GetResult();
#pragma warning restore TUnitAssertions0002
            attempts++;
            return batchCapacity > 0 && attempts % (batchCapacity + 1) != 0;
        };

        // Act
        var batches = (await producer.CreateBatchesAsync(messages, default)).ToArray();
        foreach (var batch in batches)
            await producer.SendAsync(batch, default);

        // Assert
        await Assert.That(batches.Length).IsEqualTo(batchCapacity == 2 ? 1 : 2);
        await Assert.That(_sender.SentMessages.Count).IsEqualTo(messages.Length);
        if (batchCapacity == 0)
            foreach (var batch in batches)
{
    await Assert.That(batch).IsTypeOf<AzureServiceBusSingleMessageBatch>();
}
        else
            foreach (var batch in batches)
{
    await Assert.That(batch).IsTypeOf<AzureServiceBusMessageBatch>();
}
        foreach (var message in messages)
        {
            var sent = await Assert.That(_sender.SentMessages).HasSingleItem(sent => sent.MessageId == message.Id.Value);
            await AssertMessage(sent, message, timeToLive);
        }
    }

    [Test]
    [Arguments(false, null)]
    [Arguments(true, null)]
    [Arguments(false, 300)]
    [Arguments(true, 300)]
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
#pragma warning disable TUnitAssertions0002 // Synchronous callback: the assertion is executed by GetResult.
            Assert.That(message.GetRawAmqpMessage().Header.TimeToLive).IsEqualTo(timeToLive).GetAwaiter().GetResult();
#pragma warning restore TUnitAssertions0002
            return message.MessageId != oversizedMessage.Id.Value;
        };

        // Act
        var batches = (await producer.CreateBatchesAsync(messages, default)).ToArray();
        foreach (var batch in batches)
            await producer.SendAsync(batch, default);

        // Assert
        await Assert.That(batches.OfType<AzureServiceBusMessageBatch>().Count()).IsEqualTo(2);
        await Assert.That(batches.OfType<AzureServiceBusSingleMessageBatch>()).HasSingleItem();
        await Assert.That(_sender.SentMessages.Count).IsEqualTo(messages.Length);
        foreach (var message in messages)
        {
            var sent = await Assert.That(_sender.SentMessages).HasSingleItem(sent => sent.MessageId == message.Id.Value);
            await AssertMessage(sent, message, timeToLive);
        }
    }

    [Test]
    public async System.Threading.Tasks.Task When_publication_time_to_live_is_not_configured_should_use_entity_default()
    {
        // Arrange
        var publication = new AzureServiceBusPublication();

        // Act
        var timeToLive = publication.TimeToLive;

        // Assert
        await Assert.That(timeToLive).IsNull();
        await Assert.That(_publication.TimeToLive).IsNull();
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(long.MinValue)]
    public async System.Threading.Tasks.Task When_publication_time_to_live_is_not_positive_should_reject_value(long ticks)
    {
        // Arrange
        var originalTimeToLive = TimeSpan.FromMinutes(5);
        _publication.TimeToLive = originalTimeToLive;

        // Act
        var exception = await Assert.That(() => _publication.TimeToLive = TimeSpan.FromTicks(ticks)).ThrowsExactly<ArgumentOutOfRangeException>();

        // Assert
        await Assert.That(exception.ParamName).IsEqualTo("value");
        await Assert.That(_publication.TimeToLive).IsEqualTo(originalTimeToLive);
    }

    [Test]
    [Arguments(1)]
    [Arguments(long.MaxValue)]
    public async System.Threading.Tasks.Task When_publication_time_to_live_is_positive_should_accept_value(long ticks)
    {
        // Arrange
        var timeToLive = TimeSpan.FromTicks(ticks);

        // Act
        _publication.TimeToLive = timeToLive;

        // Assert
        await Assert.That(_publication.TimeToLive).IsEqualTo(timeToLive);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
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
        await Assert.That(_sender.SentMessages).HasCount().EqualTo(2);
        await AssertMessage(_sender.SentMessages[0], firstMessage, timeToLive);
        await AssertMessage(_sender.SentMessages[1], secondMessage, null);

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

    private static async System.Threading.Tasks.Task AssertMessage(ServiceBusMessage sent, Message original, TimeSpan? timeToLive)
    {
        await Assert.That(sent.GetRawAmqpMessage().Header.TimeToLive).IsEqualTo(timeToLive);
        await Assert.That(sent.TimeToLive).IsEqualTo(timeToLive ?? TimeSpan.MaxValue);
        await Assert.That(sent.MessageId).IsEqualTo(original.Id.Value);
        await Assert.That(sent.Body.ToString()).IsEqualTo(original.Body.Value);
    }
}
