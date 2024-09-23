using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Core.Tests.FeatureSwitch.TestDoubles;
using Paramore.Brighter.FeatureSwitch;
using Paramore.Brighter.FeatureSwitch.Providers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Context;

public class RequestContextTests 
{
    [Fact]
    public void When_Accessing_A_Request_Context()
    {
        //arrange
        var builder = Sdk.CreateTracerProviderBuilder();

        var exportedActivities = new List<Activity>();
        var traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(exportedActivities)
            .Build(); 
        
        var activitySource = new ActivitySource("Paramore.Brighter.Tests");
        var span = activitySource.StartActivity();

        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test"), MessageType.MT_COMMAND),
            new MessageBody("test content"));
        
        //act
        
        var context = new RequestContext
        {
            FeatureSwitches = FluentConfigRegistryBuilder
                .With()
                .StatusOf<MyFeatureSwitchedConfigHandler>()
                .Is(FeatureSwitchStatus.On)
                .Build(),
            Policies = new PolicyRegistry{
                { "key", Policy.NoOp() }
            }
        };
        context.Bag.AddOrUpdate("key", "value", (key, oldValue) => "value");
        context.Span = span;
        context.OriginatingMessage = message; 
        
        //assert
       Assert.Equal(context.Bag["key"], "value");
       Assert.NotNull(context.Policies["key"]);
       Assert.Equal(span?.Id, context.Span?.Id);  
       Assert.NotNull(context.OriginatingMessage);
       Assert.Equal(context.OriginatingMessage.Header.MessageId, message.Header.MessageId);
        
    }
}
