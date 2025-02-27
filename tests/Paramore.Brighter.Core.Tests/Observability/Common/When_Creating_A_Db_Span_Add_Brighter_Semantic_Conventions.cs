using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            new MessageHeader(Guid.NewGuid().ToString(), new("MyTopic"), MessageType.MT_COMMAND), 
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
        Assert.True(flushed);

        //check the created activity
        Assert.Equal(_parentActivity.Id, childActivity.ParentId);
        Assert.Equal($"{OutboxDbOperation.Add.ToSpanName()} {InMemoryAttributes.DbName} {InMemoryAttributes.DbTable}", childActivity.DisplayName);
        Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId);
        Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.DbName);
        Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == OutboxDbOperation.Add.ToSpanName());
        Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
        Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.MySql.ToDbName());
        Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement);
        Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa");
        Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80");
        Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306");
        Assert.Contains(childActivity.TagObjects, t => t.Key == BrighterSemanticConventions.NetworkPeerPort && (int)t.Value == 3306);
        Assert.Contains(childActivity.TagObjects, t => t.Key == BrighterSemanticConventions.ServerPort && (int)t.Value == 3306);

        //check via the exporter as well
        Assert.Equal(2, _exportedActivities.Count);
        Assert.Contains(_exportedActivities, a => a.Source.Name == BrighterSemanticConventions.SourceName);
        var childSpan = _exportedActivities.First(a => a.DisplayName == $"{OutboxDbOperation.Add.ToSpanName()} {InMemoryAttributes.DbName} {InMemoryAttributes.DbTable}");
        Assert.NotNull(childSpan);
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId);
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == OutboxDbOperation.Add.ToSpanName());
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.MySql.ToDbName());
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement);
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa");
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80");
        Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306");
        Assert.Contains(childSpan.TagObjects, t => t.Key == BrighterSemanticConventions.ServerPort && (int)t.Value == 3306);
        Assert.Contains(childSpan.TagObjects, t => t.Key == BrighterSemanticConventions.NetworkPeerPort && (int)t.Value == 3306);
        
    }
}
