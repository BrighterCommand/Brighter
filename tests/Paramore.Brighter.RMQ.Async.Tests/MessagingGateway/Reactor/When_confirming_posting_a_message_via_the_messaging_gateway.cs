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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqMessageProducerConfirmationsSendMessageTests : IDisposable
{
    private readonly RmqMessageProducer _messageProducer;
    private readonly Message _message;
    private bool _messageWasPublished = false;
    private bool _messageWasNotPublished = true;

    public RmqMessageProducerConfirmationsSendMessageTests ()
    {
        _message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey(Guid.NewGuid().ToString()), 
                MessageType.MT_COMMAND), 
            new MessageBody("test content"));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
        _messageProducer.OnMessagePublished += (success, guid) =>
        {
            if (success)
            {
                Assert.Equal(_message.Id, guid);
                _messageWasPublished = true;
                _messageWasNotPublished = false;
            }
            else
            {
                _messageWasNotPublished = true;
            }
        };

        //we need a queue to avoid a discard
        new QueueFactory(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(_message.Header.Topic))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public async Task When_confirming_posting_a_message_via_the_messaging_gateway()
    {
        _messageProducer.Send(_message);

        await Task.Delay(500);

        //if this is true, then possible test failed because of timeout or RMQ issues
        Assert.False(_messageWasNotPublished);
        //did we see the message - intent to test logic here
        Assert.True(_messageWasPublished);
    }

    public void Dispose()
    {
        _messageProducer.Dispose();
    }
}
