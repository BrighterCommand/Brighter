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

namespace Paramore.Brighter.Core.Tests.Context;
[NotInParallel]
public class RequestContextTests
{
    [Test]
    public async Task When_Accessing_A_Request_Context()
    {
        //arrange
        var builder = Sdk.CreateTracerProviderBuilder();
        var exportedActivities = new List<Activity>();
        var traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(exportedActivities).Build();
        var activitySource = new ActivitySource("Paramore.Brighter.Tests");
        var span = activitySource.StartActivity();
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new RoutingKey("test"), MessageType.MT_COMMAND), new MessageBody("test content"));
        //act
        var context = new RequestContext
        {
            FeatureSwitches = FluentConfigRegistryBuilder.With().StatusOf<MyFeatureSwitchedConfigHandler>().Is(FeatureSwitchStatus.On).Build(),
            Policies = new PolicyRegistry
            {
                {
                    "key",
                    Policy.NoOp()
                }
            }
        };
        context.Bag.AddOrUpdate("key", "value", (key, oldValue) => "value");
        context.Span = span;
        context.OriginatingMessage = message;
        //assert
        await Assert.That(context.Bag["key"]).IsEqualTo("value");
        await Assert.That(context.Policies["key"]).IsNotNull();
        await Assert.That(context.Span?.Id).IsEqualTo(span?.Id);
        await Assert.That(context.OriginatingMessage).IsNotNull();
        await Assert.That(message.Header.MessageId).IsEqualTo(context.OriginatingMessage.Header.MessageId);
    }
}