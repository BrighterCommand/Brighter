﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
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
        const string topic = "MyTopic";
        const string paritionKey = "MyPartitionKey";
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_COMMAND, partitionKey: paritionKey), 
            new MessageBody("test content")
        );

        var publication = new Publication() { Topic = new RoutingKey(topic) };
        
        //act
        BrighterTracer.CreateMapperEvent(message, publication, _parentActivity, "MyMessageMapper", false, true);
        
        _parentActivity.Stop();
        var flushed = _traceProvider.ForceFlush();
        
        //assert
        //check the created activity
        var childActivity = _exportedActivities.First(a => a.DisplayName == "BrighterSemanticConventionsMessageMapperTests");
        childActivity.Should().NotBeNull();
        var childEvent = childActivity.Events.First(e => e.Name == "MyMessageMapper");
        
        //assert
        childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MapperName && (string)t.Value == "MyMessageMapper").Should().BeTrue();
        childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MapperType && (string)t.Value == "sync").Should().BeTrue();
        childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.IsSink && (bool)t.Value == true).Should().BeTrue();
        childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestination && t.Value?.ToString() == topic.ToString()).Should().BeTrue();
        childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageId && (string)t.Value == message.Id).Should().BeTrue();
        childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessagingDestinationPartitionId && (string)t.Value == paritionKey).Should().BeTrue();
        childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageBody && (string)t.Value == message.Body.Value).Should().BeTrue();
        childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageBodySize && (int)t.Value == message.Body.Value.Length).Should().BeTrue();
        childEvent.Tags.Any(t => t.Key == BrighterSemanticConventions.MessageHeaders && (string)t.Value == JsonSerializer.Serialize(message.Header)).Should().BeTrue();
    }
}
