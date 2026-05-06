using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.Common;
[NotInParallel]
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
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
        _tracer = new BrighterTracer();
        _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterSemanticConventionsDbSpanTests");
    }

    [Test]
    [Arguments(InstrumentationOptions.All)]
    [Arguments(InstrumentationOptions.DatabaseInformation)]
    [Arguments(InstrumentationOptions.None)]
    public async Task When_Creating_A_Db_Span_Add_Brighter_Semantic_Conventions(InstrumentationOptions options)
    {
        //arrange
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new("MyTopic"), MessageType.MT_COMMAND), new MessageBody("test content"));
        //act
        var dbInstanceId = Guid.NewGuid().ToString();
        var childActivity = _tracer.CreateDbSpan(new BoxSpanInfo(dbSystem: DbSystem.MySql, dbName: InMemoryAttributes.OutboxDbName, dbOperation: BoxDbOperation.Add, dbTable: InMemoryAttributes.DbTable, serverPort: 3306, dbInstanceId: dbInstanceId, dbStatement: DbStatement, dbUser: "sa", networkPeerAddress: "10.1.2.80", networkPeerPort: 3306, serverAddress: "http://localhost:3306"), _parentActivity, options: options);
        childActivity.Stop();
        _parentActivity.Stop();
        var flushed = _traceProvider.ForceFlush();
        //assert
        await Assert.That(flushed).IsTrue();
        //check the created activity
        await Assert.That(childActivity.ParentId).IsEqualTo(_parentActivity.Id);
        await Assert.That(childActivity.DisplayName).IsEqualTo($"{BoxDbOperation.Add.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        if (options == InstrumentationOptions.None)
            await Assert.That(childActivity.Tags).IsEmpty();
        if (options.HasFlag(InstrumentationOptions.DatabaseInformation))
        {
            await Assert.That(childActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId);
            await Assert.That(childActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName);
            await Assert.That(childActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Add.ToSpanName());
            await Assert.That(childActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
            await Assert.That(childActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.MySql.ToDbName());
            await Assert.That(childActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement);
            await Assert.That(childActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa");
            await Assert.That(childActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80");
            await Assert.That(childActivity.Tags).Contains(t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306");
            await Assert.That(childActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.NetworkPeerPort && (int)t.Value == 3306);
            await Assert.That(childActivity.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.ServerPort && (int)t.Value == 3306);
        }
        else
        {
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbInstanceId);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbName);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbOperation);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbTable);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbSystem);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbStatement);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbUser);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.ServerAddress);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.NetworkPeerPort);
            await Assert.That(childActivity.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.ServerPort);
        }

        //check via the exporter as well
        await Assert.That(_exportedActivities.Count).IsEqualTo(2);
        await Assert.That(_exportedActivities).Contains(a => a.Source.Name == BrighterSemanticConventions.SourceName);
        var childSpan = _exportedActivities.First(a => a.DisplayName == $"{BoxDbOperation.Add.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        await Assert.That(childSpan).IsNotNull();
        if (options.HasFlag(InstrumentationOptions.DatabaseInformation))
        {
            await Assert.That(childSpan.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId);
            await Assert.That(childSpan.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Add.ToSpanName());
            await Assert.That(childSpan.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable);
            await Assert.That(childSpan.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == DbSystem.MySql.ToDbName());
            await Assert.That(childSpan.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement);
            await Assert.That(childSpan.Tags).Contains(t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa");
            await Assert.That(childSpan.Tags).Contains(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80");
            await Assert.That(childSpan.Tags).Contains(t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306");
            await Assert.That(childSpan.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.ServerPort && (int)t.Value == 3306);
            await Assert.That(childSpan.TagObjects).Contains(t => t.Key == BrighterSemanticConventions.NetworkPeerPort && (int)t.Value == 3306);
        }
        else
        {
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbInstanceId);
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbOperation);
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbTable);
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbSystem);
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbStatement);
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.DbUser);
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress);
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.ServerAddress);
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.ServerPort);
            await Assert.That(childSpan.Tags).DoesNotContain(t => t.Key == BrighterSemanticConventions.NetworkPeerPort);
        }
    }
}