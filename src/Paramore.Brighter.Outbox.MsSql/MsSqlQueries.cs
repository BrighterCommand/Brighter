namespace Paramore.Brighter.Outbox.MsSql
{
    public class MsSqlQueries : IRelationDatabaseOutboxQueries
    {
        public string PagedDispatchedCommand { get; } = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY [Timestamp] DESC) AS NUMBER, * FROM {0}) AS TBL WHERE [Dispatched] IS NOT NULL AND [Dispatched] < @DispatchedSince AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY [Timestamp] DESC";
        public string PagedReadCommand { get; } = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY [Timestamp] DESC) AS NUMBER, * FROM {0}) AS TBL WHERE NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
        public string PagedOutstandingCommand { get; } = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY [Timestamp] ASC) AS NUMBER, * FROM {0} WHERE [Dispatched] IS NULL {1} ) AS TBL WHERE [Timestamp] < @DispatchedSince AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) {1} ORDER BY [Timestamp] ASC";
        public string PagedOutstandingCommandInStatement { get; } = "AND [Topic] NOT IN ( {0} )";
        public string AddCommand { get; } =
            "INSERT INTO {0} ([MessageId],[MessageType],[Topic],[Timestamp],[CorrelationId]," +
            "[ReplyTo],[ContentType],[PartitionKey],[HeaderBag],[Body]," +
            "[Source],[Type],[DataSchema],[Subject],[TraceParent],[TraceState],[Baggage]) " +
            "VALUES (@MessageId,@MessageType,@Topic,@Timestamp,@CorrelationId," +
            "@ReplyTo,@ContentType,@PartitionKey,@HeaderBag,@Body," +
            "@Source,@Type,@DataSchema,@Subject,@TraceParent,@TraceState,@Baggage)";
        public string BulkAddCommand { get; } =
            "INSERT INTO {0} ([MessageId],[MessageType],[Topic],[Timestamp],[CorrelationId]," +
            "[ReplyTo],[ContentType],[PartitionKey],[HeaderBag],[Body]," +
            "[Source],[Type],[DataSchema],[Subject],[TraceParent],[TraceState],[Baggage]) " +
            "VALUES {1}";
        public string MarkDispatchedCommand { get; } = "UPDATE {0} SET [Dispatched] = @DispatchedAt WHERE [MessageId] = @MessageId";
        public string MarkMultipleDispatchedCommand { get; } = "UPDATE {0} SET [Dispatched] = @DispatchedAt WHERE [MessageId] IN ( {1} )";
        public string GetMessageCommand { get; } = "SELECT * FROM {0} WHERE [MessageId] = @MessageId";
        public string GetMessagesCommand { get; } = "SELECT * FROM {0} WHERE [MessageId] IN ( {1} )";
        public string DeleteMessagesCommand { get; } = "DELETE FROM {0} WHERE [MessageId] IN ( {1} )";
        public string GetNumberOfOutstandingMessagesCommand { get; } = "SELECT COUNT(1) FROM {0} WHERE [Dispatched] IS NULL";
    }
}
