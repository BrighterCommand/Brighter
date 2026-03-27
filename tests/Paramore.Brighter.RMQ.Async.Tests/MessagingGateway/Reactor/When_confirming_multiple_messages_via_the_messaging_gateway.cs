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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Reactor;

[Trait("Category", "RMQ")]
[Collection("RMQ")]
public class RmqMessageProducerConfirmationsMultipleMessagesTests : IDisposable
{
    private readonly RmqMessageProducer _messageProducer;
    private readonly List<Message> _messages;
    private readonly int _numberOfMessages = 10;
    private int _totalPublished = 0;

    public RmqMessageProducerConfirmationsMultipleMessagesTests()
    {
        _messages = new List<Message>();
        var routingKey = new RoutingKey(Guid.NewGuid().ToString());

        for (int i = 0; i < _numberOfMessages; i++)
        {
            _messages.Add(new Message(
                new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND),
                new MessageBody($"test content {i}")));
        }

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);

        new QueueFactory(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), new RoutingKeys(routingKey))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public async Task When_confirming_multiple_messages_via_the_messaging_gateway()
    {
        // Subscribe to the OnMessagePublished event
        _messageProducer.OnMessagePublished += (success, messageId) =>
        {
            Interlocked.Increment(ref _totalPublished);
        };

        // Send all messages
        for (int i = 0; i < _messages.Count; i++)
        {
            _messageProducer.Send(_messages[i]);
        }

        // Wait for confirmations
        await Task.Delay(1000);

        // Verify that OnMessagePublished was called for all messages
        Assert.Equal(_numberOfMessages, _totalPublished);
    }

    public void Dispose()
    {
        _messageProducer.Dispose();
    }
}
