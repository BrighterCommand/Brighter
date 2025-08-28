#region Licence

/* The MIT License (MIT)
Copyright © 2025 Jakub Syty <jakub.nekro@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter.Outbox.PostgreSql
{
    public class PostgreSqlQueries : IRelationDatabaseOutboxQueries
    {
        public string PagedDispatchedCommand { get; } = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE DISPATCHED IS NOT NULL AND DISPATCHED < (CURRENT_TIMESTAMP + (@OutstandingSince || ' millisecond')::INTERVAL) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
        public string PagedReadCommand { get; } = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
        public string PagedOutstandingCommand { get; } = "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp ASC) AS NUMBER, * FROM {0} WHERE DISPATCHED IS NULL) AS TBL WHERE TIMESTAMP < (CURRENT_TIMESTAMP + (@OutstandingSince || ' millisecond')::INTERVAL) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp ASC";
        public string AddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp::timestamptz, @CorrelationId, @ReplyTo, @ContentType, @HeaderBag, @Body)";
        public string BulkAddCommand { get; } = "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES {1}";
        public string MarkDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = @DispatchedAt WHERE MessageId = @MessageId";
        public string MarkMultipleDispatchedCommand { get; } = "UPDATE {0} SET Dispatched = @DispatchedAt WHERE MessageId in ( {1} )";
        public string GetMessageCommand { get; } = "SELECT * FROM {0} WHERE MessageId = @MessageId";
        public string GetMessagesCommand { get; } = "SELECT * FROM {0} WHERE MessageId IN ( {1} )";
        public string DeleteMessagesCommand { get; } = "DELETE FROM {0} WHERE MessageId IN ( {1} )";
        public string DispatchedCommand { get; } = "SELECT * FROM {0} WHERE Dispatched is not NULL and Dispatched < (CURRENT_TIMESTAMP + (@DispatchedSince || ' hour')::INTERVAL) ORDER BY Dispatched LIMIT @PageSize";
        public string GetNumberOfOutstandingMessagesCommand { get; } = "SELECT count(1) FROM {0} where Dispatched is NULL";
    }
}
