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
using Amazon;
using Amazon.Runtime;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

public class SqsSchedulerSelectionTests
{
    [Theory]
    [InlineData(SqsType.Standard, false, false)]
    [InlineData(SqsType.Standard, false, true)]
    [InlineData(SqsType.Standard, true, false)]
    [InlineData(SqsType.Standard, true, true)]
    [InlineData(SqsType.Fifo, false, false)]
    [InlineData(SqsType.Fifo, false, true)]
    [InlineData(SqsType.Fifo, true, false)]
    [InlineData(SqsType.Fifo, true, true)]
    public async Task When_sending_with_an_available_sqs_scheduler_should_schedule_the_original_message(
        SqsType queueType, bool useAsyncSend, bool useAsyncScheduler)
    {
        //Arrange
        var recorder = new InMemoryDelayedMessageScheduler();
        IAmAMessageScheduler scheduler = useAsyncScheduler
            ? new InMemoryAsyncOnlyMessageScheduler(recorder)
            : new InMemorySyncOnlyMessageScheduler(recorder);
        using var producer = CreateProducer(queueType, scheduler);
        var message = CreateMessage();
        var delay = DelayFor(queueType);
        using var cancellation = new CancellationTokenSource();

        //Act
        if (useAsyncSend)
            await producer.SendWithDelayAsync(message, delay, cancellation.Token);
        else
            producer.SendWithDelay(message, delay);

        //Assert
        Assert.Same(message, recorder.ScheduledMessage);
        Assert.Equal(delay, recorder.ScheduledDelay);
        Assert.Equal(useAsyncScheduler, recorder.UsedAsync);
        Assert.Equal(useAsyncSend && useAsyncScheduler ? cancellation.Token : CancellationToken.None,
            recorder.CancellationToken);
    }

    [Theory]
    [InlineData(SqsType.Standard, false, false)]
    [InlineData(SqsType.Standard, false, true)]
    [InlineData(SqsType.Standard, true, false)]
    [InlineData(SqsType.Standard, true, true)]
    [InlineData(SqsType.Fifo, false, false)]
    [InlineData(SqsType.Fifo, false, true)]
    [InlineData(SqsType.Fifo, true, false)]
    [InlineData(SqsType.Fifo, true, true)]
    public async Task When_sending_without_a_usable_sqs_scheduler_should_explain_the_configuration_error(
        SqsType queueType, bool useAsyncSend, bool markerOnly)
    {
        //Arrange
        using var producer = CreateProducer(queueType,
            markerOnly ? new InMemoryUnsupportedMessageScheduler() : null);
        var message = CreateMessage();
        var delay = DelayFor(queueType);

        //Act
        var exception = useAsyncSend
            ? await Assert.ThrowsAsync<ConfigurationException>(() => producer.SendWithDelayAsync(message, delay))
            : Assert.Throws<ConfigurationException>(() => producer.SendWithDelay(message, delay));

        //Assert
        Assert.Contains("SqsMessageProducer", exception.Message);
        Assert.Contains("MessageSchedulerFactory", exception.Message);
    }

    [Theory]
    [InlineData(SqsType.Standard, false)]
    [InlineData(SqsType.Standard, true)]
    [InlineData(SqsType.Fifo, false)]
    [InlineData(SqsType.Fifo, true)]
    public async Task When_both_sqs_scheduler_interfaces_are_available_should_prefer_the_calling_interface(
        SqsType queueType, bool useAsyncSend)
    {
        //Arrange
        var scheduler = new InMemoryDelayedMessageScheduler();
        using var producer = CreateProducer(queueType, scheduler);
        var message = CreateMessage();
        var delay = DelayFor(queueType);
        using var cancellation = new CancellationTokenSource();

        //Act
        if (useAsyncSend)
            await producer.SendWithDelayAsync(message, delay, cancellation.Token);
        else
            producer.SendWithDelay(message, delay);

        //Assert
        Assert.Same(message, scheduler.ScheduledMessage);
        Assert.Equal(delay, scheduler.ScheduledDelay);
        Assert.Equal(useAsyncSend, scheduler.UsedAsync);
        Assert.Equal(useAsyncSend ? cancellation.Token : CancellationToken.None, scheduler.CancellationToken);
    }

    [Theory]
    [InlineData(SqsType.Standard)]
    [InlineData(SqsType.Fifo)]
    public async Task When_async_sqs_scheduling_is_cancelled_should_propagate_cancellation(SqsType queueType)
    {
        //Arrange
        var recorder = new InMemoryDelayedMessageScheduler();
        using var producer = CreateProducer(queueType, new InMemoryAsyncOnlyMessageScheduler(recorder));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        //Act
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => producer.SendWithDelayAsync(CreateMessage(), DelayFor(queueType), cancellation.Token));

        //Assert
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Null(recorder.ScheduledMessage);
    }

    private static SqsMessageProducer CreateProducer(SqsType queueType, IAmAMessageScheduler? scheduler)
    {
        var connection = new AWSMessagingGatewayConnection(
            new BasicAWSCredentials("test", "test"), RegionEndpoint.EUWest1);
        var queueName = queueType == SqsType.Fifo ? "scheduler-test.fifo" : "scheduler-test";
        return new SqsMessageProducer(connection, new SqsPublication
        {
            Topic = new RoutingKey(queueName),
            ChannelName = new ChannelName($"https://sqs.eu-west-1.amazonaws.com/000000000000/{queueName}"),
            FindQueueBy = QueueFindBy.Url,
            MakeChannels = OnMissingChannel.Assume,
            QueueAttributes = new SqsAttributes(type: queueType)
        })
        {
            Scheduler = scheduler
        };
    }

    private static TimeSpan DelayFor(SqsType queueType)
        => queueType == SqsType.Fifo ? TimeSpan.FromSeconds(5) : TimeSpan.FromMinutes(16);

    private static Message CreateMessage() => new(
        new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("scheduler-test"), MessageType.MT_EVENT),
        new MessageBody("scheduled content"));
}
