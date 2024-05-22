namespace Paramore.Brighter.Observability;

public static class BrighterSemanticConventions
{
    public const string AddToOutbox = "paramore.brighter.add_to_outbox";
    public const string DbName = "db.name";
    public const string DbOperation = "db.operation";
    public const string DbSystem = "db.system";
    public const string HandlerName = "paramore.brighter.handler.name";
    public const string HandlerType = "paramore.brighter.handler.type";
    public const string IsSink = "paramore.brighter.is_sink";
    public const string MapperName = "paramore.brighter.mapper.name";
    public const string MapperType = "paramore.brighter.mapper.type";
    public const string MessageBody = "messaging.message.body";
    public const string MessageHeaders = "messaging.message.headers";
    public const string MessageId = "messaging.message.id";
    public const string MessagingDestination = "messaging.destination";
    public const string MessagingDestinationPartitionId = "messaging.destination.partition.id";
    public const string MessageBodySize = "messaging.message.body.size";
    public const string Operation = "paramore.brighter.operation";
    public const string OutboxSharedTransaction = "paramore.brighter.outbox.shared_transaction";
    public const string OutboxType = "paramore.brighter.outbox.type";
    public const string RequestId = "paramore.brighter.request.id"; 
    public const string RequestType = "paramore.brighter.request.type"; 
    public const string RequestBody = "paramore.brighter.request.body"; 
    public const string SourceName = "paramore.brighter";
}
