namespace Paramore.Brighter.Outbox.PostgreSql
{
    public class PostgreSqlQueries : IRelationDatabaseOutboxQueries, IRelationalDatabaseOutboxCausationQueries
    {
        // All column are created with lower case because during table creating we didn't scape colum
        public string PagedDispatchedCommand { get; } = "SELECT * FROM {0} WHERE \"dispatched\" IS NOT NULL AND \"dispatched\" < @DispatchedSince ORDER BY \"timestamp\" DESC LIMIT @Take OFFSET @Skip";
        public string PagedReadCommand { get; } = "SELECT * FROM {0} ORDER BY \"timestamp\" ASC LIMIT @Take OFFSET @Skip";
        public string PagedOutstandingCommand { get; } = "SELECT * FROM {0} WHERE \"dispatched\" IS NULL AND \"timestamp\" < @TimestampSince {1} ORDER BY \"timestamp\" DESC LIMIT @Take OFFSET @Skip";
        public string PagedOutstandingCommandInStatement { get; } = "AND \"topic\" NOT IN ( {0} )";
        public string AddCommand { get; } =
            "INSERT INTO {0} " +
            "(\"messageid\",\"messagetype\",\"topic\",\"timestamp\",\"correlationid\",\"replyto\",\"contenttype\",\"partitionkey\",\"headerbag\",\"body\"" +
            ",\"source\",\"type\",\"dataschema\",\"subject\",\"traceparent\",\"tracestate\",\"baggage\", \"workflowid\",\"jobid\") " +
            "VALUES (@MessageId,@MessageType,@Topic,@Timestamp,@CorrelationId,@ReplyTo,@ContentType,@PartitionKey,@HeaderBag,@Body" +
            ",@Source,@Type,@DataSchema,@Subject,@TraceParent,@TraceState,@Baggage, @WorkflowId,@JobId)";

        public string BulkAddCommand { get; } =
            "INSERT INTO {0} " +
            "(\"messageid\",\"messagetype\",\"topic\",\"timestamp\",\"correlationid\",\"replyto\",\"contenttype\",\"partitionkey\",\"headerbag\",\"body\"" +
            ",\"source\",\"type\",\"dataschema\",\"subject\",\"traceparent\",\"tracestate\",\"baggage\", \"workflowid\",\"jobid\") " +
            "VALUES {1}";
        public string MarkDispatchedCommand { get; } = "UPDATE {0} SET \"dispatched\" = @DispatchedAt WHERE \"messageid\" = @MessageId";
        public string MarkMultipleDispatchedCommand { get; } = "UPDATE {0} SET \"dispatched\" = @DispatchedAt WHERE \"messageid\" IN ( {1} )";
        public string GetMessageCommand { get; } = "SELECT * FROM {0} WHERE \"messageid\" = @MessageId";
        public string GetMessagesCommand { get; } = "SELECT * FROM {0} WHERE \"messageid\" IN ( {1} ) ORDER BY \"timestamp\" ASC";
        public string DeleteMessagesCommand { get; } = "DELETE FROM {0} WHERE \"messageid\" IN ( {1} )";
        public string GetNumberOfOutstandingMessagesCommand { get; } = "SELECT COUNT(1) FROM {0} WHERE \"dispatched\" IS NULL";

        public string AddCausationCommand { get; } =
            "INSERT INTO {0} " +
            "(\"messageid\",\"messagetype\",\"topic\",\"timestamp\",\"correlationid\",\"replyto\",\"contenttype\",\"partitionkey\",\"headerbag\",\"body\"" +
            ",\"source\",\"type\",\"dataschema\",\"subject\",\"traceparent\",\"tracestate\",\"baggage\", \"workflowid\",\"jobid\",\"causationid\") " +
            "VALUES (@MessageId,@MessageType,@Topic,@Timestamp,@CorrelationId,@ReplyTo,@ContentType,@PartitionKey,@HeaderBag,@Body" +
            ",@Source,@Type,@DataSchema,@Subject,@TraceParent,@TraceState,@Baggage, @WorkflowId,@JobId,@CausationId)";
        public string BulkAddCausationCommand { get; } =
            "INSERT INTO {0} " +
            "(\"messageid\",\"messagetype\",\"topic\",\"timestamp\",\"correlationid\",\"replyto\",\"contenttype\",\"partitionkey\",\"headerbag\",\"body\"" +
            ",\"source\",\"type\",\"dataschema\",\"subject\",\"traceparent\",\"tracestate\",\"baggage\", \"workflowid\",\"jobid\",\"causationid\") " +
            "VALUES {1}";
        public string ReplayCausationCommand { get; } = "UPDATE {0} SET \"dispatched\" = NULL WHERE \"causationid\" = @CausationId";
        public string CausationColumnExistsCommand { get; } = "SELECT 1 FROM pg_attribute WHERE attrelid = to_regclass('{0}') AND attname = 'causationid' AND NOT attisdropped";
    }
}
