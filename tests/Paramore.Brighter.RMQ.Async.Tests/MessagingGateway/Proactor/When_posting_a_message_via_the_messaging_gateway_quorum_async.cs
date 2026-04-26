#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway.Proactor;

[Category("RMQ")]
public class RmqMessageProducerSendMessageQuorumTestsAsync : IAsyncDisposable
{
    private readonly IAmAMessageProducerAsync _messageProducer;
    private readonly IAmAMessageConsumerAsync _messageConsumer;
    private readonly Message _message;

    public RmqMessageProducerSendMessageQuorumTestsAsync()
    {
        var messageId = Id.Random();
        var topic = new RoutingKey(Guid.NewGuid().ToString());
        var messageType = MessageType.MT_COMMAND;
        var source = new Uri("http://testing.example");
        var type = "test-type";
        var timestamp = DateTimeOffset.UtcNow;
        var correlationId = Id.Random();
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
            Exchange = new Exchange("paramore.brighter.durableexchange", durable: true)
        };

        _messageProducer = new RmqMessageProducer(rmqConnection);
        var queueName = new ChannelName(Guid.NewGuid().ToString());
            
        // Create consumer with Quorum queue type, ensuring durability is enabled
        // The consumer will handle queue creation automatically
        _messageConsumer = new RmqMessageConsumer(rmqConnection, queueName, _message.Header.Topic, 
            isDurable: true, // Required for quorum queues
            highAvailability: false, // Not supported for quorum queues
            queueType: QueueType.Quorum);
    }

    [Test]
    public async Task When_posting_a_message_via_the_messaging_gateway_with_quorum_queue()
    {
        //we need to do this to create the queue 
        await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(100)); 
        
        // Send the message
        await _messageProducer.SendAsync(_message);

        // Give quorum queue a moment to become consistent across replicas
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        var result = (await _messageConsumer.ReceiveAsync(TimeSpan.FromMilliseconds(10000))).First(); 

        // Assert message body
        await Assert.That(result.Body.Value).IsEqualTo(_message.Body.Value);

        // Assert header values
        await Assert.That(result.Header.MessageId).IsEqualTo(_message.Header.MessageId);
        await Assert.That(result.Header.Topic).IsEqualTo(_message.Header.Topic);
        await Assert.That(result.Header.MessageType).IsEqualTo(_message.Header.MessageType);
        await Assert.That(result.Header.Source).IsEqualTo(_message.Header.Source);
        await Assert.That(result.Header.Type).IsEqualTo(_message.Header.Type);
        await Assert.That(result.Header.TimeStamp).IsEqualTo(_message.Header.TimeStamp).Within(TimeSpan.FromSeconds(1));
        await Assert.That(result.Header.CorrelationId).IsEqualTo(_message.Header.CorrelationId);
        await Assert.That(result.Header.ReplyTo).IsEqualTo(_message.Header.ReplyTo);
        await Assert.That(result.Header.ContentType).IsEqualTo(_message.Header.ContentType);
        await Assert.That(result.Header.HandledCount).IsEqualTo(_message.Header.HandledCount);
        await Assert.That(result.Header.DataSchema).IsEqualTo(_message.Header.DataSchema);
        await Assert.That(result.Header.Subject).IsEqualTo(_message.Header.Subject);
        await Assert.That(result.Header.Delayed).IsEqualTo(TimeSpan.Zero);                                //we clear any delay from the producer, as it represents delay in the pipeline
        await Assert.That(result.Header.TraceParent).IsEqualTo(_message.Header.TraceParent);
        await Assert.That(result.Header.TraceState).IsEqualTo(_message.Header.TraceState);
        await Assert.That(result.Header.Baggage).IsEqualTo(_message.Header.Baggage);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        ((IAmAMessageProducerSync)_messageProducer).Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _messageProducer.DisposeAsync();
        await _messageConsumer.DisposeAsync();
    }
}
