#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

// NOTE (FR-9 / FR-2 verification strategy): the enriched PublishConfirmationResult (id +
// message.Header.Topic + captured ActivityContext) is populated identically on both
// OnPublishSucceeded (ack) and OnPublishFailed (nack) — the same _pendingConfirmations entry feeds
// both handlers. Only the ack path is deterministically reachable against a real broker, so the
// enrichment is asserted there. The nack-branch enrichment is therefore verified indirectly: the
// failure raise reads the very same entry, so it carries the same id/topic/context.
[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqConfirmationCarriesIdTopicAndContextTests : IDisposable
{
    private readonly RmqMessageProducer _messageProducer;
    private readonly Message _message;
    private readonly ActivitySource _activitySource;
    private readonly ActivityListener _listener;
    private readonly TaskCompletionSource<PublishConfirmationResult> _confirmation =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RmqConfirmationCarriesIdTopicAndContextTests()
    {
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()),
                MessageType.MT_COMMAND),
            new MessageBody("test content"));

        // A real, recording ActivitySource so that Activity.Current carries a populated context at send time.
        _activitySource = new ActivitySource("Paramore.Brighter.RMQ.Sync.Tests.Confirmation");
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source == _activitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
        _messageProducer.OnMessagePublished += result => _confirmation.TrySetResult(result);

        //we need a queue to avoid a discard
        new QueueFactory(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(_message.Header.Topic))
            .Create(TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public async Task When_a_confirmation_is_received_should_carry_id_topic_and_context()
    {
        // Arrange — an Activity is current at the moment of send, so the producer can capture its context.
        using var publishActivity = _activitySource.StartActivity("publish");
        Assert.NotNull(publishActivity);

        // Act
        _messageProducer.Send(_message);

        var confirmed = await Task.WhenAny(_confirmation.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(ReferenceEquals(confirmed, _confirmation.Task), "Timed out waiting for the broker confirmation");
        var result = await _confirmation.Task;

        // Assert
        Assert.True(result.Success);
        Assert.Equal(_message.Id, result.MessageId);
        Assert.Equal(_message.Header.Topic, result.Topic);
        Assert.NotNull(result.PublishSpanContext);
        Assert.Equal(publishActivity!.TraceId, result.PublishSpanContext!.Value.TraceId);
        Assert.Equal(publishActivity.SpanId, result.PublishSpanContext.Value.SpanId);
    }

    public void Dispose()
    {
        _messageProducer.Dispose();
        _listener.Dispose();
        _activitySource.Dispose();
    }
}
