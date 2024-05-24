using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Observability.Common;

public class BrighterSemanticConventionsDbSpanTests
{
    private const string DbStatement = "INSERT INTO outbox (id, topic, message_id, message_body, message_type, time_to_live, created_at_utc) VALUES (@id, @topic, @message_id, @message_body, @message_type, @time_to_live, @created_at_utc)";
    private readonly ICollection<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Activity _parentActivity;
    private readonly BrighterTracer _tracer;

    public BrighterSemanticConventionsDbSpanTests()
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        _exportedActivities = new List<Activity>();

        _traceProvider = builder
            .AddSource("Paramore.Brighter.Tests", "Paramore.Brighter")
            .ConfigureResource(r => r.AddService("in-memory-tracer"))
            .AddInMemoryExporter(_exportedActivities)
            .Build();
        
        _tracer = new BrighterTracer();
        _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterSemanticConventionsDbSpanTests"); 
    }
    
    [Fact]
    public void When_Creating_A_Db_Span_Add_Brighter_Semantic_Conventions()
    {
        //arrange
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), "MyTopic", MessageType.MT_COMMAND), 
            new MessageBody("test content")
        );

        //act
        var dbInstanceId = Guid.NewGuid().ToString();
        var childActivity = _tracer.CreateDbSpan(
            new OutboxSpanInfo(
                dbSystem: DbSystem.MySql, 
                dbName: InMemoryAttributes.DbName, 
                dbOperation: OutboxDbOperation.Add, 
                dbTable: InMemoryAttributes.DbTable, 
                serverPort:3306,
                dbInstanceId: dbInstanceId,
                dbStatement: DbStatement,
                dbUser: "sa",
                networkPeerAddress: "10.1.2.80",
                networkPeerPort: 3306, 
                serverAddress: "http://localhost:3306"
                ),
            _parentActivity,
            options: InstrumentationOptions.All
        );
        
        childActivity.Stop();
        _parentActivity.Stop();
        
        var flushed = _traceProvider.ForceFlush();

        //assert
        flushed.Should().BeTrue();

        //check the created activity
        childActivity.ParentId.Should().Be(_parentActivity.Id);
        childActivity.DisplayName.Should().Be($"{OutboxDbOperation.Add.ToSpanName()} {InMemoryAttributes.DbName} {InMemoryAttributes.DbTable}");
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId);
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.DbName);
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == OutboxDbOperation.Add.ToSpanName());
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.MySql.ToDbName());
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement);
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa");
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80");
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.NetworkPeerPort && t.Value == "3306");
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306");
        childActivity.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.ServerPort && t.Value == "3306");
        
        //check via the exporter as well
        _exportedActivities.Count.Should().Be(2);
        _exportedActivities.Any(a => a.Source.Name == BrighterSemanticConventions.SourceName).Should().BeTrue();
        var childSpan = _exportedActivities.First(a => a.DisplayName == $"{OutboxDbOperation.Add.ToSpanName()} {InMemoryAttributes.DbName} {InMemoryAttributes.DbTable}");
        childSpan.Should().NotBeNull();
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId);
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == OutboxDbOperation.Add.ToSpanName());
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.MySql.ToDbName());
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement);
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa");
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80");
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.NetworkPeerPort && t.Value == "3306");
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306");
        childSpan.Tags.Should().Contain(t => t.Key == BrighterSemanticConventions.ServerPort && t.Value == "3306");
        
    }
}
