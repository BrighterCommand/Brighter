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
using System.Linq;
using System.Net.Mime;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqMessageProducerDelayedMessageTests : IDisposable
{
    private readonly IAmAMessageProducerSync _messageProducer;
    private readonly IAmAMessageConsumerSync _messageConsumer;
    private readonly Message _message;

    public RmqMessageProducerDelayedMessageTests()
    {
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
            
        var header = new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND);
        header.Bag.Add(HeaderNames.DELAY_MILLISECONDS, 1000);
        _message = new Message(header, new MessageBody("test3 content", new ContentType(MediaTypeNames.Text.Plain)));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.delay.brighter.exchange", supportDelay: true)
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
            
        var queueName = new ChannelName(Guid.NewGuid().ToString());
            
        _messageConsumer = new RmqMessageConsumer(rmqConnection, queueName, routingKey, false);

        new QueueFactory(rmqConnection, queueName, new RoutingKeys([routingKey]))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public void When_reading_a_delayed_message_via_the_messaging_gateway()
    {
        _messageProducer.SendWithDelay(_message, TimeSpan.FromMilliseconds(3000));

        var immediateResult = _messageConsumer.Receive(TimeSpan.Zero).First();
        var deliveredWithoutWait = immediateResult.Header.MessageType == MessageType.MT_NONE;
        Assert.Equal(0, immediateResult.Header.HandledCount);
        Assert.Equal(TimeSpan.Zero, immediateResult.Header.Delayed);

        //_should_have_not_been_able_get_message_before_delay
        Assert.True(deliveredWithoutWait);
            
        var delayedResult = _messageConsumer.Receive(TimeSpan.FromMilliseconds(10000)).First();

        //_should_send_a_message_via_rmq_with_the_matching_body
        Assert.Equal(_message.Body.Value, delayedResult.Body.Value);
        Assert.Equal(MessageType.MT_COMMAND, delayedResult.Header.MessageType);
        Assert.Equal(0, delayedResult.Header.HandledCount);
        Assert.Equal(TimeSpan.FromMilliseconds(3000), delayedResult.Header.Delayed);

        _messageConsumer.Acknowledge(delayedResult);
    }

    [Fact]
    public void When_requeing_a_failed_message_with_delay()
    {
        //send & receive a message
        _messageProducer.Send(_message);
        var message = _messageConsumer.Receive(TimeSpan.FromMilliseconds(1000)).Single();
        Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
        Assert.Equal(0, message.Header.HandledCount);
        Assert.Equal(TimeSpan.FromMilliseconds(0), message.Header.Delayed);

        _messageConsumer.Acknowledge(message);

        //now requeue with a delay
        _message.Header.UpdateHandledCount();
        _messageConsumer.Requeue(_message, TimeSpan.FromMilliseconds(1000));

        //receive and assert
        var message2 = _messageConsumer.Receive(TimeSpan.FromMilliseconds(5000)).Single();
        Assert.Equal(MessageType.MT_COMMAND, message2.Header.MessageType);
        Assert.Equal(1, message2.Header.HandledCount);

        _messageConsumer.Acknowledge(message2);
    }

    public void Dispose()
    {
        _messageConsumer.Dispose();
        _messageProducer.Dispose();
    }
}
