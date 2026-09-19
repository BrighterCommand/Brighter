#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway;

/// <summary>
/// A delayed send has no native SNS equivalent, so the producer hands the message to the configured
/// scheduler. Until the sync path was fixed to pass its <c>delay</c> through, that branch could not
/// be reached from <see cref="SnsMessageProducer.SendWithDelay"/> at all, and the unguarded cast it
/// contains had never run.
/// </summary>
/// <remarks>
/// <para>
/// With no scheduler configured the cast dereferenced a null and raised
/// <see cref="NullReferenceException"/>; with a scheduler implementing only the other half of the
/// interface pair it raised <see cref="InvalidCastException"/>. Neither names the setting the
/// operator has to change, and both arrive from inside a send rather than from configuration.
/// </para>
/// <para>
/// Every other gateway that cannot delay natively - Redis, Kafka, MsSql, MQTT, and the in-memory
/// reference implementation - answers this with a <see cref="ConfigurationException"/> naming
/// <c>MessageSchedulerFactory</c>, and accepts a scheduler that implements either half. These tests
/// hold SNS to the same contract on both the sync and async paths.
/// </para>
/// </remarks>
public class SnsMessageProducerMissingSchedulerTests
{
    private static readonly TimeSpan s_delay = TimeSpan.FromSeconds(30);

    private static SnsMessageProducer ProducerWith(IAmAMessageScheduler? scheduler)
    {
        var connection = new AWSMessagingGatewayConnection(
            new BasicAWSCredentials("test", "test"), RegionEndpoint.EUWest1);

        // A topic ARN is supplied so the producer never needs to reach AWS to resolve one; every
        // assertion here is about the branch taken before any client call.
        return new SnsMessageProducer(connection,
            new SnsPublication { TopicArn = "arn:aws:sns:eu-west-1:000000000000:test-topic" })
        {
            Scheduler = scheduler
        };
    }

    private static Message AMessage() => new(
        new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test.topic"), MessageType.MT_EVENT),
        new MessageBody("test content"));

    [Fact]
    public void When_sending_with_delay_and_no_scheduler_should_throw_configuration_exception()
    {
        var producer = ProducerWith(null);

        var exception = Assert.Throws<ConfigurationException>(
            () => producer.SendWithDelay(AMessage(), s_delay));

        Assert.Contains("no scheduler is configured", exception.Message);
        Assert.Contains("MessageSchedulerFactory", exception.Message);
    }

    [Fact]
    public async Task When_sending_async_with_delay_and_no_scheduler_should_throw_configuration_exception()
    {
        var producer = ProducerWith(null);

        var exception = await Assert.ThrowsAsync<ConfigurationException>(
            () => producer.SendWithDelayAsync(AMessage(), s_delay));

        Assert.Contains("no scheduler is configured", exception.Message);
        Assert.Contains("MessageSchedulerFactory", exception.Message);
    }

    [Fact]
    public void When_sending_with_delay_and_only_an_async_scheduler_should_schedule_it()
    {
        // The sync path used to cast straight to IAmAMessageSchedulerSync, so an async-only
        // scheduler - what an async host configures - failed with InvalidCastException.
        var scheduler = new AsyncOnlyScheduler();
        var producer = ProducerWith(scheduler);

        producer.SendWithDelay(AMessage(), s_delay);

        Assert.Equal(s_delay, scheduler.ScheduledDelay);
    }

    [Fact]
    public async Task When_sending_async_with_delay_and_only_a_sync_scheduler_should_schedule_it()
    {
        var scheduler = new SyncOnlyScheduler();
        var producer = ProducerWith(scheduler);

        await producer.SendWithDelayAsync(AMessage(), s_delay);

        Assert.Equal(s_delay, scheduler.ScheduledDelay);
    }

    private sealed class SyncOnlyScheduler : IAmAMessageSchedulerSync
    {
        public TimeSpan? ScheduledDelay { get; private set; }

        public string Schedule(Message message, TimeSpan delay)
        {
            ScheduledDelay = delay;
            return "scheduled";
        }

        public string Schedule(Message message, DateTimeOffset at) => throw new NotSupportedException();
        public bool ReScheduler(string schedulerId, DateTimeOffset at) => throw new NotSupportedException();
        public bool ReScheduler(string schedulerId, TimeSpan delay) => throw new NotSupportedException();
        public void Cancel(string id) => throw new NotSupportedException();
    }

    private sealed class AsyncOnlyScheduler : IAmAMessageSchedulerAsync
    {
        public TimeSpan? ScheduledDelay { get; private set; }

        public Task<string> ScheduleAsync(Message message, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            ScheduledDelay = delay;
            return Task.FromResult("scheduled");
        }

        public Task<string> ScheduleAsync(Message message, DateTimeOffset at, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> ReSchedulerAsync(string schedulerId, DateTimeOffset at, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> ReSchedulerAsync(string schedulerId, TimeSpan delay, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task CancelAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
