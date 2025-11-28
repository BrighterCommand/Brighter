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

public class BrighterSemanticConventionsDbSpanWithCustomNameTests
{
    private const string DbStatement = "INSERT INTO outbox (id, topic, message_id, message_body, message_type, time_to_live, created_at_utc) VALUES (@id, @topic, @message_id, @message_body, @message_type, @time_to_live, @created_at_utc)";
    private readonly ICollection<Activity> _exportedActivities;
    private readonly TracerProvider _traceProvider;
    private readonly Activity _parentActivity;
    private readonly BrighterTracer _tracer;

    public BrighterSemanticConventionsDbSpanWithCustomNameTests()
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
    
    [Theory]
    [InlineData(InstrumentationOptions.All)]
    [InlineData(InstrumentationOptions.DatabaseInformation)]
    [InlineData(InstrumentationOptions.None)]
    public void When_Creating_A_Db_Span_With_Custom_Name_Add_Brighter_Semantic_Conventions(InstrumentationOptions options)
    {
        //arrange
        const string databaseSystemName = "some-datatabase";
        var message = new Message(
            new MessageHeader(Guid.NewGuid().ToString(), new("MyTopic"), MessageType.MT_COMMAND), 
            new MessageBody("test content")
        );

        //act
        var dbInstanceId = Guid.NewGuid().ToString();
        var childActivity = _tracer.CreateDbSpan(
            new BoxSpanInfo(
                dbSystemName: databaseSystemName,
                dbName: InMemoryAttributes.OutboxDbName, 
                dbOperation: BoxDbOperation.Add, 
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
            options: options
        );
        
        childActivity.Stop();
        _parentActivity.Stop();
        
        var flushed = _traceProvider.ForceFlush();

 
        //assert
        Assert.True(flushed);

        //check the created activity
        Assert.Equal(_parentActivity.Id, childActivity.ParentId);
        Assert.Equal($"{BoxDbOperation.Add.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}", childActivity.DisplayName);
        if(options == InstrumentationOptions.None)
            Assert.Empty(childActivity.Tags);
        if (options.HasFlag(InstrumentationOptions.DatabaseInformation))
        {
            Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId);
            Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);
            Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Add.ToSpanName());
            Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
            Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == databaseSystemName);
            Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement);
            Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa");
            Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80");
            Assert.Contains(childActivity.Tags, t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306");
            Assert.Contains(childActivity.TagObjects, t => t.Key == BrighterSemanticConventions.NetworkPeerPort && (int)t.Value == 3306);
            Assert.Contains(childActivity.TagObjects, t => t.Key == BrighterSemanticConventions.ServerPort && (int)t.Value == 3306);

        }
        else
        {
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbInstanceId);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbName);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbOperation);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbTable);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbSystem);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbStatement);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.DbUser);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.NetworkPeerAddress);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.ServerAddress);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.NetworkPeerPort);
            Assert.DoesNotContain(childActivity.Tags, t => t.Key == BrighterSemanticConventions.ServerPort);
        }

        //check via the exporter as well
        Assert.Equal(2, _exportedActivities.Count);
        Assert.Contains(_exportedActivities, a => a.Source.Name == BrighterSemanticConventions.SourceName);
        var childSpan = _exportedActivities.First(a => a.DisplayName == $"{BoxDbOperation.Add.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        Assert.NotNull(childSpan);

        if (options.HasFlag(InstrumentationOptions.DatabaseInformation))
        {
            Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId);
            Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Add.ToSpanName());
            Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
            Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == databaseSystemName);
            Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement);
            Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa");
            Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80");
            Assert.Contains(childSpan.Tags, t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306");
            Assert.Contains(childSpan.TagObjects, t => t.Key == BrighterSemanticConventions.ServerPort && (int)t.Value == 3306);
            Assert.Contains(childSpan.TagObjects, t => t.Key == BrighterSemanticConventions.NetworkPeerPort && (int)t.Value == 3306);
        }
        else
        {
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbInstanceId);
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbOperation);
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbTable);
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbSystem);
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbStatement);
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.DbUser);
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.NetworkPeerAddress);
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.ServerAddress);
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.ServerPort);
            Assert.DoesNotContain(childSpan.Tags, t => t.Key == BrighterSemanticConventions.NetworkPeerPort);
        }
    }
}
