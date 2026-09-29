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
        _traceProvider = builder.AddSource("Paramore.Brighter.Tests", "Paramore.Brighter").ConfigureResource(r => r.AddService("in-memory-tracer")).AddInMemoryExporter(_exportedActivities).Build();
        _tracer = new BrighterTracer();
        _parentActivity = new ActivitySource("Paramore.Brighter.Tests").StartActivity("BrighterSemanticConventionsDbSpanTests");
    }

    [Test]
    [Arguments(InstrumentationOptions.All)]
    [Arguments(InstrumentationOptions.DatabaseInformation)]
    [Arguments(InstrumentationOptions.None)]
    public async Task When_Creating_A_Db_Span_With_Custom_Name_Add_Brighter_Semantic_Conventions(InstrumentationOptions options)
    {
        //arrange
        const string databaseSystemName = "some-datatabase";
        var message = new Message(new MessageHeader(Guid.NewGuid().ToString(), new("MyTopic"), MessageType.MT_COMMAND), new MessageBody("test content"));
        //act
        var dbInstanceId = Guid.NewGuid().ToString();
        var childActivity = _tracer.CreateDbSpan(new BoxSpanInfo(dbSystemName: databaseSystemName, dbName: InMemoryAttributes.OutboxDbName, dbOperation: BoxDbOperation.Add, dbTable: InMemoryAttributes.DbTable, serverPort: 3306, dbInstanceId: dbInstanceId, dbStatement: DbStatement, dbUser: "sa", networkPeerAddress: "10.1.2.80", networkPeerPort: 3306, serverAddress: "http://localhost:3306"), _parentActivity, options: options);
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
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId)).IsTrue();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbName && t.Value == InMemoryAttributes.OutboxDbName)).IsTrue();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Add.ToSpanName())).IsTrue();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable)).IsTrue();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == databaseSystemName)).IsTrue();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement)).IsTrue();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa")).IsTrue();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80")).IsTrue();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306")).IsTrue();
            await Assert.That((childActivity.TagObjects).Any(t => t.Key == BrighterSemanticConventions.NetworkPeerPort && (int)t.Value == 3306)).IsTrue();
            await Assert.That((childActivity.TagObjects).Any(t => t.Key == BrighterSemanticConventions.ServerPort && (int)t.Value == 3306)).IsTrue();
        }
        else
        {
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbInstanceId)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbName)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbOperation)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbTable)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbSystem)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbStatement)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.DbUser)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.ServerAddress)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.NetworkPeerPort)).IsFalse();
            await Assert.That((childActivity.Tags).Any(t => t.Key == BrighterSemanticConventions.ServerPort)).IsFalse();
        }

        //check via the exporter as well
        await Assert.That(_exportedActivities.Count).IsEqualTo(2);
        await Assert.That((_exportedActivities).Any(a => a.Source.Name == BrighterSemanticConventions.SourceName)).IsTrue();
        var childSpan = _exportedActivities.First(a => a.DisplayName == $"{BoxDbOperation.Add.ToSpanName()} {InMemoryAttributes.OutboxDbName} {InMemoryAttributes.DbTable}");
        await Assert.That(childSpan).IsNotNull();
        if (options.HasFlag(InstrumentationOptions.DatabaseInformation))
        {
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbInstanceId && t.Value == dbInstanceId)).IsTrue();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbOperation && t.Value == BoxDbOperation.Add.ToSpanName())).IsTrue();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbTable && t.Value == InMemoryAttributes.DbTable)).IsTrue();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbSystem && t.Value == databaseSystemName)).IsTrue();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbStatement && t.Value == DbStatement)).IsTrue();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbUser && t.Value == "sa")).IsTrue();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress && t.Value == "10.1.2.80")).IsTrue();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.ServerAddress && t.Value == "http://localhost:3306")).IsTrue();
            await Assert.That((childSpan.TagObjects).Any(t => t.Key == BrighterSemanticConventions.ServerPort && (int)t.Value == 3306)).IsTrue();
            await Assert.That((childSpan.TagObjects).Any(t => t.Key == BrighterSemanticConventions.NetworkPeerPort && (int)t.Value == 3306)).IsTrue();
        }
        else
        {
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbInstanceId)).IsFalse();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbOperation)).IsFalse();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbTable)).IsFalse();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbSystem)).IsFalse();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbStatement)).IsFalse();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.DbUser)).IsFalse();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.NetworkPeerAddress)).IsFalse();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.ServerAddress)).IsFalse();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.ServerPort)).IsFalse();
            await Assert.That((childSpan.Tags).Any(t => t.Key == BrighterSemanticConventions.NetworkPeerPort)).IsFalse();
        }
    }
}
