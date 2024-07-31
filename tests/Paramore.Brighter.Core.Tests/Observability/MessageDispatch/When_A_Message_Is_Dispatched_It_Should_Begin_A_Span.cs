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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.MessageDispatch
{
    public class MessagePumpDispatchObservabilityTests
    {
        private const string Topic = "MyTopic";
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new();
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private readonly List<Activity> _exportedActivities;
        private readonly TracerProvider _traceProvider;

        public MessagePumpDispatchObservabilityTests()
        {
            var builder = Sdk.CreateTracerProviderBuilder();
            _exportedActivities = new List<Activity>();

            _traceProvider = builder
                .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
                .ConfigureResource(r => r.AddService("in-memory-tracer"))
                .AddInMemoryExporter(_exportedActivities)
                .Build();
        
            Brighter.CommandProcessor.ClearServiceBus();
            
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();

            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(_receivedMessages));
            
            var timeProvider  = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            var instrumentationOptions = InstrumentationOptions.All;
            
            var commandProcessor = new Brighter.CommandProcessor(
                subscriberRegistry,
                handlerFactory, 
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry(),
                tracer: tracer,
                instrumentationOptions: instrumentationOptions);

            var provider = new CommandProcessorProvider(commandProcessor);
            
            PipelineBuilder<MyEvent>.ClearPipelineCache();

            var channel = new Channel(Topic, new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, 1000));
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(
                    _ => new MyEventMessageMapper()),
                null); 
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            
            _messagePump = new MessagePumpBlocking<MyEvent>(provider, messageMapperRegistry, null, 
                new InMemoryRequestContextFactory(), tracer, instrumentationOptions)
            {
                Channel = channel, TimeoutInMilliseconds = 5000
            };

            var message = new Message(
                new MessageHeader(_myEvent.Id, Topic, MessageType.MT_EVENT), 
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );
            
            channel.Enqueue(message);
            var quitMessage = new Message(new MessageHeader(string.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            channel.Enqueue(quitMessage);
        }

        [Fact]
        public void When_a_message_is_dispatched_it_should_begin_a_span()
        {
            _messagePump.Run();
            
            _exportedActivities.Count.Should().Be(1);
            _exportedActivities.Any(a => a.Source.Name == "Paramore.Brighter").Should().BeTrue();
        }
    }
}
