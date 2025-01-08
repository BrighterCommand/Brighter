namespace Paramore.Brighter.Outbox.MySql
{
    public class MySqlQueries : IRelationDatabaseOutboxQueries
    { 
        public string PagedDispatchedCommand { get; } = "SELECT * FROM {0} AS TBL WHERE `Dispatched` IS NOT NULL AND `Dispatched` < DATE_ADD(UTC_TIMESTAMP(), INTERVAL @OutstandingSince MICROSECOND) ORDER BY `Timestamp` DESC LIMIT @PageSize OFFSET (@PageNumber - 1) * @PageSize";
        public string PagedReadCommand { get; } = "SELECT * FROM {0} AS TBL WHERE `CreatedID` BETWEEN ((?PageNumber-1)*?PageSize+1) AND (?PageNumber*?PageSize) ORDER BY Timestamp ASC";
        public string PagedOutstandingCommand { get; } = "SELECT * FROM {0} WHERE DISPATCHED IS NULL AND Timestamp < DATE_ADD(UTC_TIMESTAMP(), INTERVAL -?OutStandingSince SECOND) ORDER BY Timestamp DESC LIMIT ?PageSize OFFSET ?OffsetValue";
        public string AddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, PartitionKey, HeaderBag, Body) VALUES (?MessageId, ?MessageType, ?Topic, ?Timestamp, ?CorrelationId, ?ReplyTo, ?ContentType, ?PartitionKey, ?HeaderBag, ?Body)";
        public string BulkAddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, PartitionKey, HeaderBag, Body) VALUES {1}";
        public string MarkDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = ?DispatchedAt WHERE MessageId = ?MessageId";
        public string MarkMultipleDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = ?DispatchedAt WHERE MessageId IN ( {1} )";
        public string GetMessageCommand { get; } = "SELECT * FROM {0} WHERE MessageId = ?MessageId";
        public string GetMessagesCommand { get; } = "SELECT * FROM {0} WHERE `MessageID` IN ( {1} )ORDER BY Timestamp ASC";
        public string DeleteMessagesCommand { get; } = "DELETE FROM {0} WHERE MessageId IN ( {1} )";
        public string DispatchedCommand { get; } = "SELECT * FROM {0} WHERE `Dispatched` IS NOT NULL AND `Dispatched` < DATE_ADD(UTC_TIMESTAMP(), INTERVAL @DispatchedSince HOUR) ORDER BY `Dispatched` LIMIT @PageSize";
        public string GetNumberOfOutstandingMessagesCommand { get; } = "Select count(1) FROM {0} where Dispatched is NULL";
    }
}
