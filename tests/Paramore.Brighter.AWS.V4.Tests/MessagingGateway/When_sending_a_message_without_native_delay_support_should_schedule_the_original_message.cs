#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway;

public class SqsDelayedSendTests
{
    [Theory]
    [InlineData(SqsType.Fifo, 0.001, false)]
    [InlineData(SqsType.Fifo, 0.001, true)]
    [InlineData(SqsType.Fifo, 5, false)]
    [InlineData(SqsType.Fifo, 5, true)]
    [InlineData(SqsType.Fifo, 900, false)]
    [InlineData(SqsType.Fifo, 900, true)]
    [InlineData(SqsType.Fifo, 901, false)]
    [InlineData(SqsType.Fifo, 901, true)]
    [InlineData(SqsType.Standard, 901, false)]
    [InlineData(SqsType.Standard, 901, true)]
    public async Task When_sending_a_message_without_native_delay_support_should_schedule_the_original_message(
        SqsType queueType, double delaySeconds, bool useAsync)
    {
        //Arrange
        var scheduler = new InMemoryDelayedMessageScheduler();
        var queueName = $"scheduled-{Guid.NewGuid():N}.fifo";
        var publication = new SqsPublication
        {
            Topic = new RoutingKey(queueName),
            ChannelName = new ChannelName(queueName),
            MakeChannels = OnMissingChannel.Validate,
            QueueAttributes = new SqsAttributes(type: queueType)
        };
        using var producer = new SqsMessageProducer(GatewayFactory.CreateFactory(), publication)
        {
            Scheduler = scheduler
        };
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), publication.Topic, MessageType.MT_EVENT),
            new MessageBody("scheduled content"));
        message.Header.PartitionKey = new PartitionKey("scheduled-group");
        var deduplicationId = Guid.NewGuid().ToString();
        message.Header.Bag[HeaderNames.DeduplicationId] = deduplicationId;
        var delay = TimeSpan.FromSeconds(delaySeconds);
        using var cancellation = new CancellationTokenSource();

        //Act
        if (useAsync)
        {
            await producer.SendWithDelayAsync(message, delay, cancellation.Token);
        }
        else
        {
            producer.SendWithDelay(message, delay);
        }

        //Assert
        Assert.Same(message, scheduler.ScheduledMessage);
        Assert.Equal(delay, scheduler.ScheduledDelay);
        Assert.Equal(useAsync, scheduler.UsedAsync);
        Assert.Equal(useAsync ? cancellation.Token : CancellationToken.None, scheduler.CancellationToken);
        Assert.Equal(new PartitionKey("scheduled-group"), scheduler.ScheduledMessage!.Header.PartitionKey);
        Assert.Equal(deduplicationId, scheduler.ScheduledMessage.Header.Bag[HeaderNames.DeduplicationId]);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(null, true, false)]
    [InlineData(0, false, false)]
    [InlineData(0, true, false)]
    [InlineData(-1, false, false)]
    [InlineData(-1, true, false)]
    [InlineData(null, false, true)]
    [InlineData(null, true, true)]
    public async Task When_sending_a_fifo_message_without_positive_delay_should_deliver_without_scheduling(
        int? delaySeconds, bool useAsync, bool ordinarySend)
    {
        //Arrange
        var provider = new SqsFifoMessageGatewayProvider();
        var publication = provider.CreatePublication(provider.GetOrCreateRoutingKey());
        var subscription = provider.CreateSubscription(publication.Topic!, provider.GetOrCreateChannelName(),
            OnMissingChannel.Create);
        var scheduler = new InMemoryDelayedMessageScheduler();
        var producer = provider.CreateProducer(publication);
        producer.Scheduler = scheduler;
        IAmAChannelSync? channel = null;
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), publication.Topic!, MessageType.MT_EVENT),
            new MessageBody("immediate content"));
        TimeSpan? delay = delaySeconds.HasValue ? TimeSpan.FromSeconds(delaySeconds.Value) : null;

        try
        {
            channel = provider.CreateChannel(subscription);

            //Act
            if (useAsync)
            {
                var asyncProducer = (IAmAMessageProducerAsync)producer;
                if (ordinarySend)
                    await asyncProducer.SendAsync(message);
                else
                    await asyncProducer.SendWithDelayAsync(message, delay);
            }
            else if (ordinarySend)
            {
                producer.Send(message);
            }
            else
            {
                producer.SendWithDelay(message, delay);
            }

            //Assert
            Assert.Null(scheduler.ScheduledMessage);
            var received = channel.Receive(TimeSpan.FromSeconds(5));
            Assert.Equal(message.Id, received.Id);
            Assert.Equal(message.Body.Value, received.Body.Value);
            channel.Acknowledge(received);
        }
        finally
        {
            provider.CleanUp(producer, channel, [message]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_sending_a_standard_message_at_the_native_delay_limit_should_not_schedule(bool useAsync)
    {
        //Arrange
        var provider = new SqsStandardMessageGatewayProvider();
        var publication = provider.CreatePublication(provider.GetOrCreateRoutingKey());
        var scheduler = new InMemoryDelayedMessageScheduler();
        var producer = provider.CreateProducer(publication);
        producer.Scheduler = scheduler;
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), publication.Topic!, MessageType.MT_EVENT),
            new MessageBody("native delayed content"));

        try
        {
            //Act
            if (useAsync)
                await ((IAmAMessageProducerAsync)producer).SendWithDelayAsync(message, TimeSpan.FromMinutes(15));
            else
                producer.SendWithDelay(message, TimeSpan.FromMinutes(15));

            //Assert
            Assert.Null(scheduler.ScheduledMessage);
        }
        finally
        {
            provider.CleanUp(producer, null, [message]);
        }
    }
}
