﻿namespace Paramore.Brighter.Outbox.MySql
{
    public class MySqlQueries : IRelationDatabaseOutboxQueries
    {
        public string PagedDispatchedCommand { get; } = "SELECT * FROM {0} AS TBL WHERE `CreatedID` BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) AND DISPATCHED IS NOT NULL AND DISPATCHED < DATE_ADD(UTC_TIMESTAMP(), INTERVAL -@OutstandingSince MICROSECOND) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
        public string PagedReadCommand { get; } = "SELECT * FROM {0} AS TBL WHERE `CreatedID` BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
        public string PagedOutstandingCommand { get; } = "SELECT * FROM {0} WHERE DISPATCHED IS NULL AND Timestamp < DATE_ADD(UTC_TIMESTAMP(), INTERVAL -@OutStandingSince SECOND) ORDER BY Timestamp DESC LIMIT @PageSize OFFSET @OffsetValue";
        public string AddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp, @CorrelationId, @ReplyTo, @ContentType, @HeaderBag, @Body)";
        public string BulkAddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES {1}";
        public string MarkDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = @DispatchedAt WHERE MessageId = @MessageId";
        public string MarkMultipleDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = @DispatchedAt WHERE MessageId IN ( {1} )";
        public string GetMessageCommand { get; } = "SELECT * FROM {0} WHERE MessageId = @MessageId";
        public string GetMessagesCommand { get; } = "SELECT * FROM {0} WHERE `MessageID` IN ( {1} )";
        public string DeleteMessagesCommand { get; } = "DELETE FROM {0} WHERE MessageId IN ( {1} )";
    }
}
