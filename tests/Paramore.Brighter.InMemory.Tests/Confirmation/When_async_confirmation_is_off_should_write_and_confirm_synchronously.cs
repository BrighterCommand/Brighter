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
using System.Threading.Tasks;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Confirmation;

public class AsyncConfirmationOffTests
{
    [Fact]
    public void When_async_confirmation_is_off_should_write_and_confirm_synchronously()
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
            UseAsyncPublishConfirmation = false
        };

        var confirmations = new List<PublishConfirmationResult>();
        producer.OnMessagePublished += confirmations.Add;

        // Act
        producer.Send(message);

        // Assert — default switch is off, write is inline, confirmation is synchronous
        Assert.False(producer.UseAsyncPublishConfirmation);
        Assert.Single(bus.Stream(new RoutingKey(topic)));
        Assert.Single(confirmations);
        Assert.True(confirmations[0].Success);
        Assert.Equal(messageId, confirmations[0].MessageId);
    }

    [Fact]
    public async Task When_async_confirmation_is_off_with_send_async_should_write_and_confirm_synchronously()
    {
        // Arrange
        const string topic = "test_topic_async";
        var messageId = Id.Random();
        var message = new Message(
            new MessageHeader(messageId, new RoutingKey(topic), MessageType.MT_DOCUMENT),
            new MessageBody("test_content"));
        var bus = new InternalBus();
        var producer = new InMemoryMessageProducer(bus, instrumentationOptions: InstrumentationOptions.All);

        var confirmations = new List<PublishConfirmationResult>();
        producer.OnMessagePublished += confirmations.Add;

        // Act
        await producer.SendAsync(message);

        // Assert — write is inline and confirmation is synchronous even via async path
        Assert.Single(bus.Stream(new RoutingKey(topic)));
        Assert.Single(confirmations);
        Assert.True(confirmations[0].Success);
        Assert.Equal(messageId, confirmations[0].MessageId);
    }
}
