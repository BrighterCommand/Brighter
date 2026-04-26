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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Category("RMQ")]
public class RmqMessageProducerDelayedMessageTestsAsync : IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly IAmAMessageConsumerAsync _messageConsumer;
    private readonly Message _message;

    public RmqMessageProducerDelayedMessageTestsAsync()
    {
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
            
        var header = new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND);
        var originalMessage = new Message(header, new MessageBody("test3 content", contentType: new ContentType(MediaTypeNames.Text.Plain)));

        var mutatedHeader = new MessageHeader(header.MessageId, routingKey, MessageType.MT_COMMAND);
        mutatedHeader.Bag.Add(HeaderNames.DELAY_MILLISECONDS, 1000);
        _message = new Message(mutatedHeader, originalMessage.Body);

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

    [Test]
    public async Task When_reading_a_delayed_message_via_the_messaging_gateway()
    {
        await _messageProducer.SendWithDelayAsync(_message, TimeSpan.FromMilliseconds(3000));

        var immediateResult = (await _messageConsumer.ReceiveAsync(TimeSpan.Zero)).First();
        var deliveredWithoutWait = immediateResult.Header.MessageType == MessageType.MT_NONE;
        await Assert.That(immediateResult.Header.HandledCount).IsEqualTo(0);
        await Assert.That(immediateResult.Header.Delayed).IsEqualTo(TimeSpan.Zero);

        //_should_have_not_been_able_get_message_before_delay
        await Assert.That(deliveredWithoutWait).IsTrue();
            
        var delayedResult = (await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000))).First();

        //_should_send_a_message_via_rmq_with_the_matching_body
        await Assert.That(delayedResult.Body.Value).IsEqualTo(_message.Body.Value);
        await Assert.That(delayedResult.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(delayedResult.Header.HandledCount).IsEqualTo(0);
        await Assert.That(delayedResult.Header.Delayed).IsEqualTo(TimeSpan.FromMilliseconds(3000));

        await _messageConsumer.AcknowledgeAsync(delayedResult);
    }

    [Test]
    public async Task When_requeing_a_failed_message_with_delay()
    {
        //send & receive a message
        await _messageProducer.SendAsync(_message);
        var message = (await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(1000))).Single();
        await Assert.That(message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message.Header.HandledCount).IsEqualTo(0);
        await Assert.That(message.Header.Delayed).IsEqualTo(TimeSpan.FromMilliseconds(0));

        await _messageConsumer.AcknowledgeAsync(message);

        //now requeue with a delay
        _message.Header.UpdateHandledCount();
        await _messageConsumer.RequeueAsync(_message, TimeSpan.FromMilliseconds(1000));

        //receive and assert
        var message2 = (await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(5000))).Single();
        await Assert.That(message2.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
        await Assert.That(message2.Header.HandledCount).IsEqualTo(1);

        await _messageConsumer.AcknowledgeAsync(message2);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        ((IAmAMessageConsumerSync)_messageConsumer).Dispose();
        ((IAmAMessageProducerSync)_messageProducer).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _messageProducer.DisposeAsync();
        await _messageConsumer.DisposeAsync();
    }
}
