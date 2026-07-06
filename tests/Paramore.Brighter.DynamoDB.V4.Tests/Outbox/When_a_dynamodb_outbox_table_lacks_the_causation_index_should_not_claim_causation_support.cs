#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Outbox.DynamoDB.V4;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

/// <summary>
/// Backward-compatibility (AC11): a DynamoDB outbox table provisioned before the Replay feature does
/// not have the <c>Causation</c> Global Secondary Index that <c>ReplayCausationAsync</c> queries.
/// <c>SupportsCausationTracking()</c> must honestly probe the live table for that GSI rather than
/// statically returning <c>true</c> — otherwise validation passes and runtime replay throws
/// "table does not have the specified index". Writes are unaffected either way.
/// </summary>
[Trait("Category", "DynamoDB")]
public sealed class DynamoDbOutboxCausationIndexProbeTests : IDisposable
{
    private const string CausationId = "causation-A";
    private readonly List<string> _tablesToDrop = [];

    [Fact]
    public void When_a_dynamodb_outbox_table_lacks_the_causation_index_should_not_claim_causation_support()
    {
        // Arrange — a pre-feature outbox table WITHOUT the Causation GSI
        var tableName = CreateOutboxTable(withCausationIndex: false);
        var outbox = OutboxFor(tableName);

        // Act / Assert — the live schema probe must report the index missing
        Assert.False(outbox.SupportsCausationTracking());
        Assert.False(outbox.SupportsCausationTrackingAsync().GetAwaiter().GetResult());
    }

    [Fact]
    public void When_replay_validated_against_a_table_without_the_causation_index_should_be_rejected()
    {
        // Arrange — Replay-configured pipeline; a tracking inbox isolates the failure to the outbox
        var tableName = CreateOutboxTable(withCausationIndex: false);
        var outbox = OutboxFor(tableName);
        var inbox = new InMemoryInbox(TimeProvider.System);

        // Act
        var findings = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            ReplayPipeline());

        // Assert — the outbox implements the role but the live table lacks the index, so validation
        // flags the un-migrated schema (locks the opt-in guard end-to-end)
        Assert.NotEmpty(findings);
        Assert.All(findings, f => Assert.Equal(ValidationSeverity.Warning, f.Severity));
        Assert.Contains(findings, f => f.Message.Contains("outbox store schema does not support"));
    }

    [Fact]
    public void When_a_dynamodb_outbox_table_lacks_the_causation_index_should_still_add()
    {
        // Arrange — without the GSI, a normal deposit must still succeed (the index is replay-only)
        var tableName = CreateOutboxTable(withCausationIndex: false);
        var outbox = OutboxFor(tableName);
        var context = ContextWithCausation(CausationId);
        var message = CreateMessage();

        // Act
        outbox.Add(message, context);
        var outstanding = outbox.OutstandingMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray();

        // Assert
        Assert.Contains(message.Id, outstanding);
    }

    [Fact]
    public void When_replaying_against_a_table_without_the_causation_index_should_not_throw()
    {
        // Arrange — a pre-feature outbox table WITHOUT the Causation GSI, holding a deposited message.
        // Models the "inbox migrated, outbox not yet" (AC10) mixed state that reaches ReplayCausation at runtime.
        var tableName = CreateOutboxTable(withCausationIndex: false);
        var outbox = OutboxFor(tableName);
        var context = ContextWithCausation(CausationId);
        outbox.Add(CreateMessage(), context);

        // Act / Assert — replay must degrade to a no-op (mirroring the relational path) rather than query the
        // absent GSI and throw a ValidationException that would unwind the handler pipeline on every duplicate.
        Assert.Null(Xunit.Record.Exception(() => outbox.ReplayCausation(CausationId, context)));
        Assert.Null(Xunit.Record.Exception(() =>
            outbox.ReplayCausationAsync(CausationId, context).GetAwaiter().GetResult()));
    }

    [Fact]
    public void When_a_dynamodb_outbox_table_has_the_causation_index_should_claim_causation_support()
    {
        // Arrange — a current outbox table WITH the Causation GSI
        var tableName = CreateOutboxTable(withCausationIndex: true);
        var outbox = OutboxFor(tableName);

        // Act / Assert — the live probe finds the index
        Assert.True(outbox.SupportsCausationTracking());
        Assert.True(outbox.SupportsCausationTrackingAsync().GetAwaiter().GetResult());
    }

    [Fact]
    public void When_a_dynamodb_outbox_table_has_the_causation_index_should_still_add()
    {
        // Arrange — regression guard: deposit must still work with the index present
        var tableName = CreateOutboxTable(withCausationIndex: true);
        var outbox = OutboxFor(tableName);
        var context = ContextWithCausation(CausationId);
        var message = CreateMessage();

        // Act
        outbox.Add(message, context);
        var outstanding = outbox.OutstandingMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray();

        // Assert
        Assert.Contains(message.Id, outstanding);
    }

    private string CreateOutboxTable(bool withCausationIndex)
    {
        var tableName = $"brighter_outbox_probe_{Uuid.New():N}";
        var request = new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                new Dictionary<string, ProvisionedThroughput?>
                {
                    ["Outstanding"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["OutstandingAllTopics"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["Delivered"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["DeliveredAllTopics"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["Causation"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                }));

        request.TableName = tableName;

        if (!withCausationIndex)
        {
            // Model a pre-feature table: drop the Causation GSI and its now-orphaned attribute
            // definition (DynamoDB rejects attribute definitions not referenced by a key schema).
            request.GlobalSecondaryIndexes.RemoveAll(gsi => gsi.IndexName == "Causation");
            request.AttributeDefinitions.RemoveAll(ad => ad.AttributeName == "CausationId");
        }

        var builder = new DynamoDbTableBuilder(Const.DynamoDbClient);
        builder.Build(request).GetAwaiter().GetResult();
        builder.EnsureTablesReady([tableName], Amazon.DynamoDBv2.TableStatus.ACTIVE).GetAwaiter().GetResult();
        _tablesToDrop.Add(tableName);
        return tableName;
    }

    private static DynamoDbOutbox OutboxFor(string tableName)
        => new(Const.DynamoDbClient, new DynamoDbConfiguration { TableName = tableName });

    private static Message CreateMessage()
        => new(
            new MessageHeader(Id.Random(), new RoutingKey("causation_topic"), MessageType.MT_DOCUMENT),
            new MessageBody("message body"));

    private static RequestContext ContextWithCausation(string causationId)
    {
        var context = new RequestContext();
        context.Bag[RequestContextBagNames.CausationId] = causationId;
        return context;
    }

    private static HandlerPipelineDescription ReplayPipeline()
        => new(
            requestType: typeof(MyCommand),
            handlerType: typeof(MyCommand),
            isAsync: false,
            beforeSteps:
            [
                new PipelineStepDescription(
                    typeof(UseInboxAttribute),
                    typeof(MyCommand),
                    Step: 0,
                    HandlerTiming.Before)
                {
                    Attribute = new UseInboxAttribute(
                        step: 0,
                        onceOnly: true,
                        onceOnlyAction: OnceOnlyAction.Replay)
                }
            ],
            afterSteps: []);

    private static IReadOnlyList<ValidationError> Evaluate(
        ISpecification<HandlerPipelineDescription> spec,
        HandlerPipelineDescription description)
    {
        spec.IsSatisfiedBy(description);
        var collector = new ValidationResultCollector<HandlerPipelineDescription>();
        return spec.Accept(collector)
            .Where(r => !r.Success)
            .Select(r => r.Error!)
            .ToList();
    }

    public void Dispose()
    {
        foreach (var table in _tablesToDrop)
        {
            try
            {
                Const.DynamoDbClient.DeleteTableAsync(table).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // best-effort cleanup
            }
        }
    }
}
