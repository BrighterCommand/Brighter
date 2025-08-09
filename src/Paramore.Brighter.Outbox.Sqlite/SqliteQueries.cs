namespace Paramore.Brighter.Outbox.Sqlite
{
    public class SqliteQueries : IRelationDatabaseOutboxQueries
    {
        public string PagedDispatchedCommand { get; } = "SELECT * FROM {0} WHERE [Dispatched] IS NOT NULL AND [Dispatched] < @DispatchedSince ORDER BY [Timestamp] DESC LIMIT @Take OFFSET @Skip";
        public string PagedReadCommand { get; } = "SELECT * FROM {0} ORDER BY [Timestamp] ASC LIMIT @Take OFFSET @Skip";
        public string PagedOutstandingCommand { get; } = "SELECT * FROM {0} WHERE [Dispatched] IS NULL AND `Timestamp` < @TimestampSince {1} ORDER BY Timestamp DESC LIMIT @Take OFFSET @Skip";
        public string PagedOutstandingCommandInStatement { get; } = "AND [Topic] NOT IN ( {0} )";
        public string AddCommand { get; } =
            "INSERT INTO {0} " +
            "(MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, PartitionKey, HeaderBag, Body, Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage, WorkflowId, JobId) " +
            "VALUES (@MessageId, @MessageType, @Topic,  @Timestamp, @CorrelationId, @ReplyTo, @ContentType, @PartitionKey,  @HeaderBag, @Body, @Source, @Type, @DataSchema, @Subject, @TraceParent, @TraceState, @Baggage, @WorkflowId, @JobId)";

        public string BulkAddCommand { get; } =
            "INSERT INTO {0} " +
            "(MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, PartitionKey,  HeaderBag, Body, Source, Type, DataSchema, Subject, TraceParent, TraceState, Baggage, WorkflowId, JobId) " +
            "VALUES {1}"; 
        public string MarkDispatchedCommand { get; } = "UPDATE {0} SET [Dispatched] = @DispatchedAt WHERE [MessageId] = @MessageId";
        public string MarkMultipleDispatchedCommand { get; } = "UPDATE {0} SET [Dispatched] = @DispatchedAt WHERE [MessageId] IN ( {1} )";
        public string GetMessageCommand { get; } = "SELECT * FROM {0} WHERE [MessageId] = @MessageId";
        public string GetMessagesCommand { get; } = "SELECT * FROM {0} WHERE [MessageId] IN ( {1} ) ORDER BY [Timestamp] ASC";
        public string DeleteMessagesCommand { get; } = "DELETE FROM {0} WHERE [MessageId] IN ( {1} )";
        public string GetNumberOfOutstandingMessagesCommand { get; } = "SELECT COUNT(1) FROM {0} WHERE [Dispatched] IS NULL";
    }
}
