#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter.Observability;

/// <summary>
/// What are the names for spans, events, and attributes in Brighter
/// </summary>
public static class BrighterSemanticConventions
{
    public const string ArchiveAge = "paramore.brighter.archive_age_in_milliseconds";
    public const string ArchiveMessages = "paramore.brighter.archive_messages";
    // BoxProvisioning migration span / event conventions. The span wraps a full
    // <c>IAmABoxMigrationRunner.MigrateAsync</c> call; <c>BoxMigrationPath</c> records
    // which of fresh/bootstrap/normal was taken after the under-lock re-detection; the
    // event names are emitted as child <see cref="System.Diagnostics.ActivityEvent"/>s
    // on the migration span so an operator can trace the path without correlating logs.
    public const string BoxMigration = "paramore.brighter.box_migration";
    public const string BoxMigrationPath = "paramore.brighter.box_migration.path";
    public const string BoxMigrationFromVersion = "paramore.brighter.box_migration.from_version";
    public const string BoxMigrationToVersion = "paramore.brighter.box_migration.to_version";
    public const string BoxMigrationEventEnsureHistory = "ensure_history_table";
    public const string BoxMigrationEventFreshInstall = "fresh_install";
    public const string BoxMigrationEventBootstrap = "bootstrap";
    public const string BoxMigrationEventNormalUpdate = "normal_update";
    public const string BoxMigrationEventHistoryTableRaceSwallowed = "history_table_race_swallowed";
    public const string BoxMigrationEventLegacyHistorySeeded = "legacy_history_seeded";
    // Attribute on the legacy_history_seeded event carrying the number of rows the D5 seed copied
    // from the legacy default-schema history to the per-schema history on a Global→PerSchema flip.
    // Operators querying a trace store can filter / aggregate by this value to size the flip impact.
    public const string BoxMigrationSeedRowCount = "brighter.box.migration.seed.rows";
    public const string BoxType = "paramore.brighter.box.type";
    public const string CausationId = "paramore.brighter.causation_id";
    public const string CeSource = "cloudevents.event_source";
    public const string CeMessageId = "cloudevents.event_id";
    public const string CeVersion = "cloudevents.event_spec_version";
    public const string ClearMessages = "paramore.brighter.clear_messages";
    public const string ConsumerGroupName = "messaging.consumer.group.name";
    public const string ConversationId = "messaging.message.conversation_id";
    public const string DbCollectionName = "db.collection.name";
    public const string DbInstanceId = "db.instance.id";
    public const string DbInstrumentationDomain = "db";
    public const string DbName = "db.name";
    public const string DbNamespace = "db.namespace";
    public const string DbOperation = "db.operation";
    public const string DbOperationName = "db.operation.name";
    public const string DbQuerySummary = "db.query.summary";
    public const string DbQueryText = "db.query.text";
    public const string DbResponseStatusCode = "db.response.status_code";
    public const string DbStatement = "db.statement";
    public const string DbSystem = "db.system";
    public const string DbTable = "db.table";
    public const string DbUser = "db.user";
    public const string ErrorType = "error.type";
    public const string HandledCount = "paramore.brighter.handled_count";
    public const string HandlerName = "paramore.brighter.handler.name";
    public const string HandlerType = "paramore.brighter.handler.type";
    public const string InstrumentationDomain = "instrumentation.domain";
    public const string IsSink = "paramore.brighter.is_sink";
    public const string MapperName = "paramore.brighter.mapper.name";
    public const string MapperType = "paramore.brighter.mapper.type";
    public const string MessageBody = "messaging.message.body";
    public const string MessageHeaders = "messaging.message.headers";
    public const string MessageId = "messaging.message.id";
    public const string MessageType = "messaging.message.type";
    public const string MessagingDestination = "messaging.destination.name";
    public const string MessagingDestinationAnonymous = "messaging.destination.anonymous";
    public const string MessagingDestinationPartitionId = "messaging.destination.partition.id";
    public const string MessagingDestinationSubscriptionName = "messaging.destination.subscription.name";
    public const string MessagingDestinationTemplate = "messaging.destination.template";
    public const string MessagingInstrumentationDomain = "messaging";
    public const string MessagingOperationName = "messaging.operation.name";
    public const string MessagingOperationType = "messaging.operation.type";
    public const string MessagingSystem = "messaging.system";
    public const string MessageBodySize = "messaging.message.body.size";
    public const string MeterName = "Paramore.Brighter";
    public const string NetworkPeerAddress = "network.peer.address";
    public const string NetworkPeerPort = "network.peer.port";
    public const string Operation = "paramore.brighter.operation";
    public const string OutboxSharedTransaction = "paramore.brighter.outbox.shared_transaction";
    public const string OutboxType = "paramore.brighter.outbox.type";
    public const string ReplyTo = "paramore.brighter.replyto";
    public const string RequestId = "paramore.brighter.request.id"; 
    public const string RequestType = "paramore.brighter.request.type"; 
    public const string RequestBody = "paramore.brighter.request.body";
    public const string ServerAddress = "server.address";
    public const string ServerPort = "server.port";
    public const string ServiceInstanceId = "service.instance.id";
    public const string ServiceName = "service.name";
    public const string ServiceNamespace = "service.namespace";
    public const string ServiceVersion = "service.version";
    public const string SourceName = "Paramore.Brighter";
    public const string CeSubject = "cloudevents.event_subject";
    public const string CeType = "cloudevents.event_type";
    public const string ClaimCheckInstrumentationDomain  = "claim_check";
    public const string ClaimCheckOperation = "claim_check.operation";
    public const string ClaimCheckProvider = "claim_check.provider";
    public const string ClaimCheckBucketName = "claim_check.bucket_name";
    public const string ClaimCheckId = "claim_check.id";
    public const string ClaimCheckContentLenght = "claim_check.content_lenght";
}
