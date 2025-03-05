using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Common;

public class BrighterSemanticConventionsMessageMapperTests
{
    private readonly ICollection<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Activity _parentActivity;
    private readonly BrighterTracer _tracer;

    public BrighterSemanticConventionsMessageMapperTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
        
        _tracer = new BrighterTracer();
        _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterSemanticConventionsMessageMapperTests");
    }
    
    [Fact]
    public void When_Creating_A_Message_Mapper_Add_Brighter_Semantic_Conventions()
    {
        //arrange
        const string paritionKey = "MyPartitionKey";
        var routingKey = new RoutingKey("MyTopic");
        
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), routingKey, MessageType.MT_COMMAND, partitionKey: paritionKey), 
            new MessageBody("test content")
        );

        var publication = new Publication() { Topic = routingKey };
        
        //act
        BrighterTracer.WriteMapperEvent(message, publication, _parentActivity, "MyMessageMapper", false, true);
        
        _parentActivity.Stop();
        var flushed = _traceProvider.ForceFlush();
        
        //assert
        //check the created activity
        var childActivity = _exportedActivities.First(a => a.DisplayName == "BrighterSemanticConventionsMessageMapperTests");
        Assert.NotNull(childActivity);
        var childEvent = childActivity.Events.First(e => e.Name == "MyMessageMapper");
        
        //assert
        Assert.True(childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MapperName && (string)t.Value == "MyMessageMapper"));
        Assert.True(childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MapperType && (string)t.Value == "sync"));
        Assert.True(childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value == true));
        Assert.True(childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestination && t.Value?.ToString() == "MyTopic".ToString()));
        Assert.True(childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && (string)t.Value == message.Id));
        Assert.True(childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)t.Value == paritionKey));
        Assert.True(childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageBody && (string)t.Value == message.Body.Value));
        Assert.True(childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageBodySize && (int)t.Value == message.Body.Value.Length));
        Assert.True(childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageHeaders && (string)t.Value == JsonSerializer.Serialize(message.Header, JsonSerialisationOptions.Options)));
    }
}
