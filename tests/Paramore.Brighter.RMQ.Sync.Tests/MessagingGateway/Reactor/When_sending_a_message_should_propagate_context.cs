using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Paramore.Brighter.Observability;
using Xunit;
using Baggage = OpenTelemetry.Baggage;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor
{
    [Trait("Category", "RMQ")]
    public class RmqMessageProducerPropagateContextTests : IDisposable
    {
        private readonly IAmAMessageProducerSync _messageProducer;
        private readonly List<Activity> _exportedActivities;
        private readonly TracerProvider _tracerProvider;
        private readonly Message _message;
        private readonly Activity? _parentActivity;

        public RmqMessageProducerPropagateContextTests()
        {
            var builder = Sdk.CreateTracerProviderBuilder();
            _exportedActivities = new List<Activity>();

            _tracerProvider = builder
                .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
                .ConfigureResource(r => r.AddService("rmq-message-producer-tracer"))
                .AddInMemoryExporter(_exportedActivities)
                .Build();

            _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("RmqMessageProducerTests");
            
            Baggage.SetBaggage("key", "value");
            Baggage.SetBaggage("key2", "value2");

            _message = new Message(
                new MessageHeader(
                    messageId: Guid.NewGuid().ToString(),
                    topic: new RoutingKey("test.topic"),
                    messageType: MessageType.MT_EVENT
                ),
                new MessageBody("test content")
            );

            var rmqConnection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
                Exchange = new Exchange("paramore.brighter.exchange")
            };

            _messageProducer = new RmqMessageProducer(rmqConnection)
            {
                Span = _parentActivity
            };
        }

        [Fact]
        public void When_Sending_A_Message_Should_Propagate_Context()
        {
            //arrange
            
            //act
            _messageProducer.Send(_message);
            _parentActivity?.Stop();
            _tracerProvider.ForceFlush();

            //assert
            var producerEvent = _exportedActivities
                .SelectMany(a => a.Events)
                .FirstOrDefault(e => e.Name == $"{_message.Header.Topic} {CommandProcessorSpanOperation.Publish.ToSpanName()}");
            
            Assert.NotNull(_message.Header.TraceParent);
            Assert.Equal("key=value,key2=value2", _message.Header.TraceState.ToString());
        }

        public void Dispose()
        {
            _messageProducer.Dispose();
            _tracerProvider.Dispose();
            _parentActivity?.Dispose();
        }
    }
}
