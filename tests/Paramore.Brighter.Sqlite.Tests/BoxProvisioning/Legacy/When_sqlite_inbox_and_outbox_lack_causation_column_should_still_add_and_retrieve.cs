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
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.Inbox;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.BoxProvisioning.Legacy;

/// <summary>
/// Backward-compatibility (AC10): a user who upgrades Brighter to a Replay-capable version but has
/// NOT migrated their inbox/outbox schema (the <c>CausationId</c> column is absent) must still be able
/// to deposit and retrieve from those stores. The causation-aware INSERT must be gated on the column
/// actually existing, not on the static driver capability interface (which every backend now implements).
/// </summary>
[Trait("Category", "Sqlite")]
public sealed class SqliteLegacySchemaCausationCompatibilityTests : IDisposable
{
    private const string CausationId = "causation-A";
    private readonly string _connectionString = Configuration.ConnectionString;
    private readonly List<string> _tablesToDrop = [];

    [Fact]
    public void When_sqlite_outbox_lacks_causation_column_should_still_add_and_retrieve()
    {
        // Arrange — a pre-feature outbox table (V7: every column except CausationId)
        var tableName = NewTableName();
        SqliteOutboxLegacySeeder.SeedAtV(7, _connectionString, tableName);
        var outbox = OutboxFor(tableName);
        var context = ContextWithCausation(CausationId);
        var message = CreateMessage();

        // Act — deposit and read back; the INSERT must not name the absent CausationId column
        outbox.Add(message, context);
        var outstanding = outbox.OutstandingMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray();

        // Assert
        Assert.Contains(message.Id, outstanding);
    }

    [Fact]
    public void When_sqlite_inbox_lacks_causation_column_should_still_add_and_retrieve()
    {
        // Arrange — a pre-feature inbox table (V2: ContextKey present, no CausationId)
        var tableName = NewTableName();
        SqliteInboxLegacySeeder.SeedAtV2(_connectionString, tableName);
        var inbox = InboxFor(tableName);
        var contextKey = Uuid.NewAsString();
        var command = new MyCommand { Value = Uuid.NewAsString() };
        var context = ContextWithCausation(CausationId);

        // Act
        inbox.Add(command, contextKey, context);
        var exists = inbox.Exists<MyCommand>(command.Id, contextKey, context);
        var retrieved = inbox.Get<MyCommand>(command.Id, contextKey, context);

        // Assert
        Assert.True(exists);
        Assert.Equal(command.Value, retrieved.Value);
    }

    [Fact]
    public void When_sqlite_inbox_lacks_causation_column_should_return_null_causation_id()
    {
        // Arrange — a pre-feature inbox table (V2: no CausationId), with a command already stored.
        // A Replay duplicate calls GetCausationId on this store; the read path must degrade gracefully
        // (return null) rather than SELECT the absent [CausationId] column and throw (AC10).
        var tableName = NewTableName();
        SqliteInboxLegacySeeder.SeedAtV2(_connectionString, tableName);
        var inbox = (IAmACausationTrackingInbox)InboxFor(tableName);
        var contextKey = Uuid.NewAsString();
        var command = new MyCommand { Value = Uuid.NewAsString() };
        var context = ContextWithCausation(CausationId);
        ((IAmAnInboxSync)inbox).Add(command, contextKey, context);

        // Act
        var causationId = inbox.GetCausationId(command.Id, contextKey, context);

        // Assert
        Assert.Null(causationId);
    }

    [Fact]
    public void When_sqlite_outbox_lacks_causation_column_should_not_support_causation_tracking()
    {
        // Arrange
        var tableName = NewTableName();
        SqliteOutboxLegacySeeder.SeedAtV(7, _connectionString, tableName);
        var outbox = (IAmACausationTrackingOutbox)OutboxFor(tableName);

        // Act / Assert — the live schema probe must report the column missing
        Assert.False(outbox.SupportsCausationTracking());
    }

    [Fact]
    public void When_sqlite_inbox_lacks_causation_column_should_not_support_causation_tracking()
    {
        // Arrange
        var tableName = NewTableName();
        SqliteInboxLegacySeeder.SeedAtV2(_connectionString, tableName);
        var inbox = (IAmACausationTrackingInbox)InboxFor(tableName);

        // Act / Assert
        Assert.False(inbox.SupportsCausationTracking());
    }

    [Fact]
    public void When_replay_validated_against_legacy_schema_stores_should_be_rejected()
    {
        // Arrange — Replay-configured pipeline against legacy (un-migrated) inbox + outbox
        var inboxTable = NewTableName();
        SqliteInboxLegacySeeder.SeedAtV2(_connectionString, inboxTable);
        var outboxTable = NewTableName();
        SqliteOutboxLegacySeeder.SeedAtV(7, _connectionString, outboxTable);

        var inbox = InboxFor(inboxTable);
        var outbox = OutboxFor(outboxTable);

        // Act
        var findings = Evaluate(
            HandlerPipelineValidationRules.ReplayRequiresCausationTracking(inbox, outbox),
            ReplayPipeline());

        // Assert — both stores implement the role but the live schema does not support it,
        // so validation flags the un-migrated schema (locks the opt-in guard end-to-end)
        Assert.NotEmpty(findings);
        Assert.All(findings, f => Assert.Equal(ValidationSeverity.Warning, f.Severity));
        Assert.Contains(findings, f => f.Message.Contains("schema does not support"));
    }

    [Fact]
    public void When_sqlite_outbox_has_causation_column_should_still_track_causation()
    {
        // Arrange — regression guard: a CURRENT-builder outbox (CausationId present) must keep tracking
        var tableName = NewTableName();
        Configuration.CreateTable(_connectionString, SqliteOutboxBuilder.GetDDL(tableName));
        _tablesToDrop.Add(tableName);
        var outbox = OutboxFor(tableName);
        var trackingOutbox = (IAmACausationTrackingOutbox)outbox;
        var context = ContextWithCausation(CausationId);
        var message = CreateMessage();

        // Act — deposit with a causation, dispatch it, then replay that causation
        outbox.Add(message, context);
        outbox.MarkDispatched(message.Id, context, DateTimeOffset.UtcNow);
        var outstandingAfterDispatch = outbox.OutstandingMessages(TimeSpan.Zero, context).Select(m => m.Id);
        trackingOutbox.ReplayCausation(CausationId, context);
        var outstandingAfterReplay = outbox.OutstandingMessages(TimeSpan.Zero, context).Select(m => m.Id).ToArray();

        // Assert — replay can only match because the CausationId was written
        Assert.True(trackingOutbox.SupportsCausationTracking());
        Assert.DoesNotContain(message.Id, outstandingAfterDispatch);
        Assert.Contains(message.Id, outstandingAfterReplay);
    }

    [Fact]
    public void When_sqlite_inbox_has_causation_column_should_still_track_causation()
    {
        // Arrange — regression guard: a CURRENT-builder inbox (CausationId present) must keep tracking
        var tableName = NewTableName();
        Configuration.CreateTable(_connectionString, SqliteInboxBuilder.GetDDL(tableName, false));
        _tablesToDrop.Add(tableName);
        var inbox = InboxFor(tableName);
        var trackingInbox = (IAmACausationTrackingInbox)inbox;
        var contextKey = Uuid.NewAsString();
        var command = new MyCommand { Value = Uuid.NewAsString() };
        var context = ContextWithCausation(CausationId);

        // Act
        inbox.Add(command, contextKey, context);
        var storedCausationId = trackingInbox.GetCausationId(command.Id, contextKey, context);

        // Assert
        Assert.True(trackingInbox.SupportsCausationTracking());
        Assert.Equal(CausationId, storedCausationId);
    }

    private string NewTableName()
    {
        var tableName = $"{Configuration.TablePrefix}{Uuid.New():N}";
        _tablesToDrop.Add(tableName);
        return tableName;
    }

    private SqliteOutbox OutboxFor(string tableName)
        => new(new RelationalDatabaseConfiguration(
            _connectionString,
            databaseName: "brightertests",
            outBoxTableName: tableName,
            binaryMessagePayload: false));

    private IAmAnInboxSync InboxFor(string tableName)
        => new SqliteInbox(new RelationalDatabaseConfiguration(_connectionString, inboxTableName: tableName));

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
            Configuration.DeleteTable(_connectionString, table);
        }
    }
}
