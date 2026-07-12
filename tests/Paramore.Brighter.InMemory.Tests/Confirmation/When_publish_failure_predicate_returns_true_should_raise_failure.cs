#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Confirmation;

public class PublishFailurePredicateTests
{
    [Fact]
    public void When_publish_failure_predicate_returns_true_should_raise_failure()
    {
        // Arrange
        const string topic = "test_topic";
        var messageId = Id.Random();
        var message = new Message(
            new MessageHeader(messageId, new RoutingKey(topic), MessageType.MT_DOCUMENT),
            new MessageBody("test_content"));
        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All)
        {
            PublishFailurePredicate = _ => true
        };

        var confirmations = new List<PublishConfirmationResult>();
        producer.OnMessagePublished += confirmations.Add;

        // Act
        producer.Send(message);

        // Assert — message NOT written to the bus; failure confirmation carries id and wire topic
        Assert.Empty(bus.Stream(new RoutingKey(topic)));
        Assert.Single(confirmations);
        Assert.False(confirmations[0].Success);
        Assert.Equal(messageId, confirmations[0].MessageId);
        Assert.Equal(new RoutingKey(topic), confirmations[0].Topic);
    }

    [Fact]
    public void When_publish_failure_predicate_is_null_should_succeed()
    {
        // Arrange — default predicate is null (never fail)
        const string topic = "test_topic_null";
        var messageId = Id.Random();
        var message = new Message(
            new MessageHeader(messageId, new RoutingKey(topic), MessageType.MT_DOCUMENT),
            new MessageBody("test_content"));
        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All);

        var confirmations = new List<PublishConfirmationResult>();
        producer.OnMessagePublished += confirmations.Add;

        // Act
        producer.Send(message);

        // Assert — null predicate behaves as success
        Assert.Single(bus.Stream(new RoutingKey(topic)));
        Assert.Single(confirmations);
        Assert.True(confirmations[0].Success);
    }

    [Fact]
    public void When_publish_failure_predicate_returns_false_should_succeed()
    {
        // Arrange — predicate explicitly passes the message through
        const string topic = "test_topic_false";
        var messageId = Id.Random();
        var message = new Message(
            new MessageHeader(messageId, new RoutingKey(topic), MessageType.MT_DOCUMENT),
            new MessageBody("test_content"));
        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All)
        {
            PublishFailurePredicate = _ => false
        };

        var confirmations = new List<PublishConfirmationResult>();
        producer.OnMessagePublished += confirmations.Add;

        // Act
        producer.Send(message);

        // Assert — false-returning predicate behaves as success
        Assert.Single(bus.Stream(new RoutingKey(topic)));
        Assert.Single(confirmations);
        Assert.True(confirmations[0].Success);
    }
}
