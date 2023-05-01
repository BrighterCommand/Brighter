namespace Paramore.Brighter.Outbox.PostgreSql
{
    public class PostgreSqlQueries : IRelationDatabaseOutboxQueries
    {
        public string PagedDispatchedCommand { get; } = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE DISPATCHED IS NOT NULL AND DISPATCHED < (CURRENT_TIMESTAMP + (@OutstandingSince || ' millisecond')::INTERVAL) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
        public string PagedReadCommand { get; } = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp ASC) AS NUMBER, * FROM {0}) AS TBL WHERE NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp ASC";
        public string PagedOutstandingCommand { get; } = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp ASC) AS NUMBER, * FROM {0} WHERE DISPATCHED IS NULL) AS TBL WHERE TIMESTAMP < (CURRENT_TIMESTAMP + (@OutstandingSince || ' millisecond')::INTERVAL) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp ASC";
        public string AddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, PartitionKey, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp::timestamptz, @CorrelationId, @ReplyTo, @ContentType, @PartitionKey,  @HeaderBag, @Body)";
        public string BulkAddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, PartitionKey, HeaderBag, Body) VALUES {1}";
        public string MarkDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = @DispatchedAt WHERE MessageId = @MessageId";
        public string MarkMultipleDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = @DispatchedAt WHERE MessageId IN ( {1} )";
        public string GetMessageCommand { get; } = "SELECT * FROM {0} WHERE MessageId = @MessageId";
        public string GetMessagesCommand { get; } = "SELECT * FROM {0} WHERE MessageID IN ( {1} )";
        public string DeleteMessagesCommand { get; }= "DELETE FROM {0} WHERE MessageId IN ( {1} )";
    }
}
