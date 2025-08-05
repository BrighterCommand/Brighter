#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

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
    /// <summary>
    /// Contains shared SQL queries for PostgreSQL outbox operations
    /// </summary>
    internal static class PostgreSqlOutboxQueries
    {
        internal const string DeleteMessageCommand = "DELETE FROM {0} WHERE MessageId IN ({1})";
        
        internal const string GetMessageByIdCommand = 
            "SELECT Id, MessageId, Topic, MessageType, Timestamp, Correlationid, ReplyTo, ContentType, HeaderBag, Body FROM {0} WHERE MessageId = @MessageId";
        
        internal const string AddMessageCommand = 
            "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES (@MessageId, @MessageType, @Topic, @Timestamp::timestamptz, @CorrelationId, @ReplyTo, @ContentType,  @HeaderBag, @Body)";
        
        internal const string BulkAddMessageCommand = 
            "INSERT INTO {0} (MessageId, MessageType, Topic, Timestamp, CorrelationId, ReplyTo, ContentType, HeaderBag, Body) VALUES {1}";
        
        internal const string MarkDispatchedCommand = 
            "UPDATE {0} SET Dispatched = @DispatchedAt WHERE MessageId = @MessageId";
        
        internal const string PagedDispatchedCommand = 
            "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE DISPATCHED IS NOT NULL AND DISPATCHED < (CURRENT_TIMESTAMP + (@OutstandingSince || ' millisecond')::INTERVAL) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
        
        internal const string PagedReadCommand = 
            "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp DESC) AS NUMBER, * FROM {0}) AS TBL WHERE NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp DESC";
        
        internal const string PagedOutstandingCommand = 
            "SELECT * FROM (SELECT ROW_NUMBER() OVER(ORDER BY Timestamp ASC) AS NUMBER, * FROM {0} WHERE DISPATCHED IS NULL) AS TBL WHERE TIMESTAMP < (CURRENT_TIMESTAMP + (@OutstandingSince || ' millisecond')::INTERVAL) AND NUMBER BETWEEN ((@PageNumber-1)*@PageSize+1) AND (@PageNumber*@PageSize) ORDER BY Timestamp ASC";
    }
}
