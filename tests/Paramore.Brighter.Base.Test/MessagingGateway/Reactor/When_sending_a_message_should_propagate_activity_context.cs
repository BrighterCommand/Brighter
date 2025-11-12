using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public void When_sending_a_message_should_propagate_activity_context()
    {
        //arrange
        var builder = Sdk.CreateTracerProviderBuilder();
        var exportedActivities = new List<Activity>();

        var tracerProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("rmq-message-producer-tracer"))
            .AddInMemoryExporter(exportedActivities)
            .Build();

        var parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("RmqMessageProducerTests");
        parentActivity!.TraceStateString = "brighter=00f067aa0ba902b7,congo=t61rcWkgMzE";
            
        Baggage.SetBaggage("key", "value");
        Baggage.SetBaggage("key2", "value2");
        
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!, GetOrCreateChannelName());
        Producer = CreateProducer(Publication);
        Channel = CreateChannel(Subscription);
        Producer.Span = parentActivity;
            
        var message = CreateMessage(Publication.Topic!, false);
        
        //act
        Producer.Send(message);
        
        parentActivity.Stop();
        tracerProvider.ForceFlush();

        //assert
        Assert.NotNull(message.Header.TraceParent);
        Assert.Equal("brighter=00f067aa0ba902b7,congo=t61rcWkgMzE", message.Header.TraceState);
        Assert.Equal("key=value,key2=value2", message.Header.Baggage.ToString());
    }
}
