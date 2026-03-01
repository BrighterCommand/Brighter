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
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
public class RmqMessageProducerDLQTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly IAmAMessageConsumerAsync _messageConsumer;
    private readonly Message _message;
    private readonly IAmAMessageConsumerAsync _deadLetterConsumer;

    public RmqMessageProducerDLQTestsAsync()
    {
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());
            
        _message = new Message(
            new MessageHeader(
                Guid.NewGuid().ToString(), 
                routingKey,
                MessageType.MT_COMMAND,
                contentType: new ContentType(MediaTypeNames.Text.Plain)), 
            new MessageBody("test content"));

        var queueName = new ChannelName(Guid.NewGuid().ToString());
        var deadLetterQueueName = new ChannelName($"{_message.Header.Topic}.DLQ");
        var deadLetterRoutingKey = new RoutingKey( $"{_message.Header.Topic}.DLQ");
            
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange"),
            DeadLetterExchange = new Exchange("paramore.brighter.exchange.dlq")
        };
            
        _messageProducer = new RmqMessageProducer(rmqConnection);

        _messageConsumer = new RmqMessageConsumer(
            connection: rmqConnection, 
            queueName: queueName, 
            routingKey: routingKey, 
            isDurable: false, 
            highAvailability: false,
            deadLetterQueueName: deadLetterQueueName,
            deadLetterRoutingKey: deadLetterRoutingKey,
            makeChannels:OnMissingChannel.Create
        );

        _deadLetterConsumer = new RmqMessageConsumer(
            connection: rmqConnection,
            queueName: deadLetterQueueName,
            routingKey: deadLetterRoutingKey,
            isDurable:false,
            makeChannels:OnMissingChannel.Assume
        );
    }

    [Fact]
    public async Task When_rejecting_a_message_to_a_dead_letter_queue()
    {
        //create the infrastructure
        await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(0)); 
            
        await _messageProducer.SendAsync(_message);

        var message = (await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000))).First(); 
            
        //This will push onto the DLQ
        await _messageConsumer.RejectAsync(message);

        var dlqMessage = (await _deadLetterConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000))).First();
            
        //assert this is our message
        Assert.Equal(_message.Id, dlqMessage.Id);
        Assert.Equal(dlqMessage.Body.Value, message.Body.Value);
    }

    public void Dispose()
    {
        ((IAmAMessageProducerSync)_messageProducer).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _messageProducer.DisposeAsync();
    }
}
