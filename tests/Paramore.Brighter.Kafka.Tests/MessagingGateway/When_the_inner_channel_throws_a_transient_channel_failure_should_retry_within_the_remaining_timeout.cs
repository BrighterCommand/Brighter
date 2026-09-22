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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

/// <summary>
/// A topic created moments before it is subscribed to may not have propagated across the cluster yet,
/// so the first consume raises a <see cref="ChannelFailureException"/> for a topic that is on its way.
/// The pump rides that out; a test that calls Receive directly has no pump, so the retryable channel
/// has to ride it out instead — but only within the caller's timeout, and only while the caller's
/// budget lasts, so a genuine outage is still reported as the exception rather than as a bare MT_NONE.
/// </summary>
[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class RetryableChannelTransientFailureTests
{
    private static readonly TimeSpan s_failureDelay = TimeSpan.FromMilliseconds(250);

    private readonly Message _delivered = new(
        new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("gen.test"), MessageType.MT_EVENT),
        new MessageBody("delivered after the topic propagated"));

    [Fact]
    public void When_the_inner_channel_throws_a_transient_channel_failure_should_retry_within_the_remaining_timeout()
    {
        //Arrange - the first receive fails, taking 250ms of the caller's 1s budget, the second delivers
        var inner = new FlakyChannelSync(failures: 1, s_failureDelay, _delivered);
        var channel = new RetryableChannelSync(inner);

        //Act
        var received = channel.Receive(TimeSpan.FromSeconds(1));

        //Assert - the message is ridden out to, and the retry drew from what was left of the budget
        Assert.Equal(_delivered.Id, received.Id);
        Assert.Equal(2, inner.ReceiveTimeouts.Count);
        Assert.True(inner.ReceiveTimeouts[1] < inner.ReceiveTimeouts[0],
            $"the retry asked for {inner.ReceiveTimeouts[1]}, which must be less than the original {inner.ReceiveTimeouts[0]}");
    }

    [Fact]
    public async Task When_the_inner_channel_throws_a_transient_channel_failure_should_retry_within_the_remaining_timeout_async()
    {
        //Arrange - the first receive fails, taking 250ms of the caller's 1s budget, the second delivers
        var inner = new FlakyChannelAsync(failures: 1, s_failureDelay, _delivered);
        var channel = new RetryableChannelAsync(inner);

        //Act
        var received = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));

        //Assert - the message is ridden out to, and the retry drew from what was left of the budget
        Assert.Equal(_delivered.Id, received.Id);
        Assert.Equal(2, inner.ReceiveTimeouts.Count);
        Assert.True(inner.ReceiveTimeouts[1] < inner.ReceiveTimeouts[0],
            $"the retry asked for {inner.ReceiveTimeouts[1]}, which must be less than the original {inner.ReceiveTimeouts[0]}");
    }

    [Fact]
    public void When_the_channel_failure_outlasts_the_timeout_should_rethrow_rather_than_report_no_message()
    {
        //Arrange - every receive fails, each taking 250ms of a 300ms budget
        var inner = new FlakyChannelSync(failures: int.MaxValue, s_failureDelay, _delivered);
        var channel = new RetryableChannelSync(inner);
        var stopwatch = Stopwatch.StartNew();

        //Act
        var exception = Record.Exception(() => channel.Receive(TimeSpan.FromMilliseconds(300)));

        //Assert - a real outage surfaces as the exception, and the retrying stops with the budget
        Assert.IsType<ChannelFailureException>(exception);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"retrying should have stopped with the budget, but took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task When_the_channel_failure_outlasts_the_timeout_should_rethrow_rather_than_report_no_message_async()
    {
        //Arrange - every receive fails, each taking 250ms of a 300ms budget
        var inner = new FlakyChannelAsync(failures: int.MaxValue, s_failureDelay, _delivered);
        var channel = new RetryableChannelAsync(inner);
        var stopwatch = Stopwatch.StartNew();

        //Act
        var exception = await Record.ExceptionAsync(() => channel.ReceiveAsync(TimeSpan.FromMilliseconds(300)));

        //Assert - a real outage surfaces as the exception, and the retrying stops with the budget
        Assert.IsType<ChannelFailureException>(exception);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"retrying should have stopped with the budget, but took {stopwatch.Elapsed}");
    }

    [Fact]
    public void When_a_channel_failure_is_followed_only_by_empty_receives_should_rethrow_rather_than_report_no_message()
    {
        //Arrange - one failure, then a topic that stays silent for the rest of the 400ms budget
        var inner = new FlakyChannelSync(failures: 1, TimeSpan.FromMilliseconds(100), Message.Empty);
        var channel = new RetryableChannelSync(inner);

        //Act
        var exception = Record.Exception(() => channel.Receive(TimeSpan.FromMilliseconds(400)));

        //Assert - the failure is only swallowed when a message actually arrives, and none did
        Assert.IsType<ChannelFailureException>(exception);
    }

    [Fact]
    public async Task When_a_channel_failure_is_followed_only_by_empty_receives_should_rethrow_rather_than_report_no_message_async()
    {
        //Arrange - one failure, then a topic that stays silent for the rest of the 400ms budget
        var inner = new FlakyChannelAsync(failures: 1, TimeSpan.FromMilliseconds(100), Message.Empty);
        var channel = new RetryableChannelAsync(inner);

        //Act
        var exception = await Record.ExceptionAsync(() => channel.ReceiveAsync(TimeSpan.FromMilliseconds(400)));

        //Assert - the failure is only swallowed when a message actually arrives, and none did
        Assert.IsType<ChannelFailureException>(exception);
    }

    /// <summary>
    /// A channel whose first <paramref name="failures"/> receives spend <paramref name="failureDelay"/>
    /// and then raise <see cref="ChannelFailureException"/>, the way a consumer does when it polls a
    /// topic the broker has not published yet; every later receive delivers <paramref name="delivered"/>.
    /// </summary>
    private sealed class FlakyChannelSync(int failures, TimeSpan failureDelay, Message delivered) : IAmAChannelSync
    {
        public List<TimeSpan?> ReceiveTimeouts { get; } = [];

        public Message Receive(TimeSpan? timeout)
        {
            ReceiveTimeouts.Add(timeout);
            if (ReceiveTimeouts.Count > failures)
                return delivered;

            Thread.Sleep(failureDelay);
            throw new ChannelFailureException("Subscribed topic not available: gen.test");
        }

        public ChannelName Name => new("gen.test");

        public RoutingKey RoutingKey => new("gen.test");

        public void Acknowledge(Message message) { }

        public void Purge() { }

        public bool Reject(Message message, MessageRejectionReason? reason = null) => true;

        public void Nack(Message message) { }

        public bool Requeue(Message message, TimeSpan? timeOut = null) => true;

        public void Enqueue(params Message[] message) { }

        public void Stop(RoutingKey topic) { }

        public void Dispose() { }
    }

    /// <summary>
    /// The <see cref="FlakyChannelSync"/> contract, over <see cref="IAmAChannelAsync"/>.
    /// </summary>
    private sealed class FlakyChannelAsync(int failures, TimeSpan failureDelay, Message delivered) : IAmAChannelAsync
    {
        public List<TimeSpan?> ReceiveTimeouts { get; } = [];

        public async Task<Message> ReceiveAsync(TimeSpan? timeout, CancellationToken cancellationToken = default)
        {
            ReceiveTimeouts.Add(timeout);
            if (ReceiveTimeouts.Count > failures)
                return delivered;

            await Task.Delay(failureDelay, cancellationToken);
            throw new ChannelFailureException("Subscribed topic not available: gen.test");
        }

        public ChannelName Name => new("gen.test");

        public RoutingKey RoutingKey => new("gen.test");

        public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PurgeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> RejectAsync(Message message, MessageRejectionReason? reason = null,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task NackAsync(Message message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> RequeueAsync(Message message, TimeSpan? timeOut = null,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public void Enqueue(params Message[] message) { }

        public void Stop(RoutingKey topic) { }

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
