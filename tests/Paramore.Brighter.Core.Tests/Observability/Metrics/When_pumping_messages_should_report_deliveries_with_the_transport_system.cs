#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Observability.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Metrics;

public class MessagePumpMetricsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_pumping_messages_should_report_deliveries_with_the_transport_system(bool useProactor)
    {
        //Arrange
        using var telemetry = new InMemoryMetricsFromTraces();
        var topic = new RoutingKey("pump-metrics");
        var request = new MyEvent();
        var message = new Message(
            new MessageHeader(request.Id, topic, MessageType.MT_EVENT),
            new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)));
        using var consumer = new InMemoryMetricsMessageConsumer(
            [new Message(), null, message, MessageFactory.CreateQuitMessage(topic)],
            MessagingSystem.ServiceBus);
        var receivedMessages = new Dictionary<string, string>();
        var subscribers = new SubscriberRegistry();
        IAmAHandlerFactory handlerFactory;
        var mappers = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));

        if (useProactor)
        {
            subscribers.RegisterAsync<MyEvent, MyEventHandlerAsync>();
            handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyEventHandlerAsync(receivedMessages));
            mappers.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        }
        else
        {
            subscribers.Register<MyEvent, MyEventHandler>();
            handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(receivedMessages));
            mappers.Register<MyEvent, MyEventMessageMapper>();
        }

        PipelineBuilder<MyEvent>.ClearPipelineCache();
        var contextFactory = new InMemoryRequestContextFactory();
        var processor = new Brighter.CommandProcessor(
            subscribers, handlerFactory, contextFactory,
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory(),
            tracer: telemetry.Tracer, instrumentationOptions: InstrumentationOptions.All);

        IAmAMessagePump pump = useProactor
            ? new Proactor(processor, _ => typeof(MyEvent), mappers, new EmptyMessageTransformerFactoryAsync(),
                contextFactory, new ChannelAsync(new ChannelName("pump-metrics"), topic, consumer), telemetry.Tracer)
                { EmptyChannelDelay = TimeSpan.Zero, ChannelFailureDelay = TimeSpan.Zero }
            : new Reactor(processor, _ => typeof(MyEvent), mappers, new EmptyMessageTransformerFactory(),
                contextFactory, new Channel(new ChannelName("pump-metrics"), topic, consumer), telemetry.Tracer)
                { EmptyChannelDelay = TimeSpan.Zero, ChannelFailureDelay = TimeSpan.Zero };

        //Act
        pump.Run();
        Assert.True(telemetry.Flush());

        //Assert
        Assert.Single(receivedMessages);
        Assert.Equal(message.Id, Assert.Single(consumer.AcknowledgedMessages));
        var pumpSpans = telemetry.Spans.Where(span =>
            span.GetTagItem(BrighterSemanticConventions.MessagingOperationType) is "begin" or "receive" or "process")
            .ToArray();
        Assert.Equal(6, pumpSpans.Length);
        Assert.All(pumpSpans, span =>
            Assert.Equal("servicebus", span.GetTagItem(BrighterSemanticConventions.MessagingSystem)));
        Assert.Equal(1, telemetry.CounterTotal("messaging.client.consumed.messages"));
        Assert.Equal(0, telemetry.CounterTotal("messaging.client.sent.messages"));
        Assert.All(telemetry.TagValues("messaging.client.consumed.messages", "messaging.system"),
            value => Assert.Equal("servicebus", value));
        Assert.All(telemetry.TagValues("messaging.client.operation.duration", "messaging.system"),
            value => Assert.Equal("servicebus", value));
        Assert.Equal(1, telemetry.HistogramCount("messaging.process.duration"));
    }
}
