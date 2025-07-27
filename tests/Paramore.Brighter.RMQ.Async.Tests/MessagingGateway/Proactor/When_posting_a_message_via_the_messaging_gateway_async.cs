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
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Trait("Category", "RMQ")]
public class RmqMessageProducerSendMessageTestsAsync : IDisposable, IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly IAmAMessageConsumerAsync _messageConsumer;
    private readonly Message _message;

    public RmqMessageProducerSendMessageTestsAsync()
    {
        var messageId = Id.Random();
        var topic = new RoutingKey(Guid.NewGuid().ToString());
        var messageType = MessageType.MT_COMMAND;
        var source = new Uri("http://testing.example");
        var type = "test-type";
        var timestamp = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid().ToString();
        var replyTo = new RoutingKey("reply-queue");
        var contentType = new ContentType(MediaTypeNames.Text.Plain){CharSet = CharacterEncoding.UTF8.FromCharacterEncoding()};
        var handledCount = 5;
        var dataSchema = new Uri("http://schema.example");
        var subject = "test-subject";
        var delayed = TimeSpan.FromSeconds(30);
        var traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        var traceState = "congo=t61rcWkgMzE";
        var baggage = new Baggage();
        baggage.LoadBaggage("userId=alice");

        _message = new Message(
            new MessageHeader(
                messageId: messageId,
                topic: topic,
                messageType: messageType,
                source: source,
                type: new CloudEventsType(type),
                timeStamp: timestamp,
                correlationId: correlationId,
                replyTo: replyTo,
                contentType: contentType,
                handledCount: handledCount,
                dataSchema: dataSchema,
                subject: subject,
                delayed: delayed,
                traceParent: traceParent,
                traceState: traceState,
                baggage: baggage),
            new MessageBody("test content", contentType: contentType));

        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
        var queueName = new ChannelName(Guid.NewGuid().ToString());
            
        _messageConsumer = new RmqMessageConsumer(rmqConnection, queueName, _message.Header.Topic, false);

        new QueueFactory(rmqConnection, queueName, new RoutingKeys(_message.Header.Topic))
            .CreateAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public async Task When_posting_a_message_via_the_messaging_gateway()
    {
        await _messageProducer.SendAsync(_message);

        var result = (await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000))).First(); 

        // Assert message body
        Assert.Equal(_message.Body.Value, result.Body.Value);

        // Assert header values
        Assert.Equal(_message.Header.MessageId, result.Header.MessageId);
        Assert.Equal(_message.Header.Topic, result.Header.Topic);
        Assert.Equal(_message.Header.MessageType, result.Header.MessageType);
        Assert.Equal(_message.Header.Source, result.Header.Source);
        Assert.Equal(_message.Header.Type, result.Header.Type);
        Assert.Equal(_message.Header.TimeStamp, result.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(_message.Header.CorrelationId, result.Header.CorrelationId);
        Assert.Equal(_message.Header.ReplyTo, result.Header.ReplyTo);
        Assert.Equal(_message.Header.ContentType, result.Header.ContentType);
        Assert.Equal(_message.Header.HandledCount, result.Header.HandledCount);
        Assert.Equal(_message.Header.DataSchema, result.Header.DataSchema);
        Assert.Equal(_message.Header.Subject, result.Header.Subject);
        Assert.Equal(TimeSpan.Zero, result.Header.Delayed);                                //we clear any delay from the producer, as it represents delay in the pipeline 
        Assert.Equal(_message.Header.TraceParent, result.Header.TraceParent);
        Assert.Equal(_message.Header.TraceState, result.Header.TraceState);
        Assert.Equal(_message.Header.Baggage, result.Header.Baggage);
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
