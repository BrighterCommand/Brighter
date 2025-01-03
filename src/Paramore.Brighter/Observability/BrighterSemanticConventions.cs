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
}
