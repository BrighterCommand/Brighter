namespace Paramore.Brighter.Outbox.Sqlite
{
    public class SqliteQueries : IRelationDatabaseOutboxQueries
    {
         public string PagedDispatchedCommand { get; } = "SELECT * FROM {0} WHERE DISPATCHED IS NOT NULL AND (strftime('%s', 'now') - strftime('%s', Dispatched)) * 1000 < @OutstandingSince ORDER BY Timestamp ASC LIMIT @PageSize OFFSET (@PageNumber-1) * @PageSize";
        public string PagedReadCommand { get; } = "SELECT * FROM {0} ORDER BY Timestamp DESC LIMIT @PageSize OFFSET (@PageNumber-1) * @PageSize";
        public string PagedOutstandingCommand { get; } = "SELECT * FROM {0} WHERE DISPATCHED IS NULL AND (strftime('%s', 'now') - strftime('%s', TimeStamp)) * 1000 > @OutstandingSince ORDER BY Timestamp ASC LIMIT @PageSize OFFSET (@PageNumber-1) * @PageSize";
        public string AddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @CorrelationId, @ReplyTo, @ContentType, @HeaderBag, @Body)";
        public string BulkAddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES {1}";
        public string MarkDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = @DispatchedAt WHERE MessageId = @MessageId";
        public string MarkMultipleDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = @DispatchedAt WHERE MessageId in ( {1} )";
        public string GetMessageCommand { get; } = "SELECT * FROM {0} WHERE MessageId = @MessageId";
        public string GetMessagesCommand { get; } = "SELECT * FROM {0} WHERE MessageId IN ( {1} )";
        public string DeleteMessagesCommand { get; } = "DELETE FROM {0} WHERE MessageId IN ( {1} )";
    }
}
