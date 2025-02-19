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
using FluentAssertions;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
public class AsyncRmqMessageConsumerOperationInterruptedTestsAsync : IAsyncDisposable, IDisposable
{
    private readonly IAmAMessageProducerAsync _sender;
    private readonly IAmAMessageConsumerAsync _receiver;
    private readonly IAmAMessageConsumerAsync _badReceiver;

    public AsyncRmqMessageConsumerOperationInterruptedTestsAsync()
    {
        var messageHeader = new MessageHeader(Guid.NewGuid().ToString(), 
            new RoutingKey(Guid.NewGuid().ToString()), MessageType.MT_COMMAND);

        messageHeader.UpdateHandledCount();
        Message sentMessage = new(messageHeader, new MessageBody("test content"));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _sender = new RmqMessageProducer(rmqConnection);
        _receiver = new RmqMessageConsumer(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), sentMessage.Header.Topic, false, false);
        _badReceiver = new OperationInterruptedRmqMessageConsumer(rmqConnection, new ChannelName(Guid.NewGuid().ToString()), sentMessage.Header.Topic, false, 1, false);

        _sender.SendAsync(sentMessage).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task  When_a_message_consumer_throws_an_operation_interrupted_exception_when_connecting()
    {
        bool exceptionHappened = false;
        try
        {
            await _badReceiver.ReceiveAsync(TimeSpan.FromMilliseconds(2000));
        }
        catch (ChannelFailureException cfe)
        {
            exceptionHappened = true;
            cfe.InnerException.Should().BeOfType<OperationInterruptedException>();
        }
            
        exceptionHappened.Should().BeTrue();
    }

    public void Dispose()
    {
        ((IAmAMessageProducerSync)_sender).Dispose();
        ((IAmAMessageConsumerSync)_receiver).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _receiver.DisposeAsync();
        await _badReceiver.DisposeAsync();
    }
}
