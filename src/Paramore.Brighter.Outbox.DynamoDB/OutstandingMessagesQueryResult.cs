using System.Collections.Generic;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class OutstandingMessagesQueryResult
    {
        public IEnumerable<MessageItem> Messages { get; private set; }
        public int ShardNumber { get; private set; }
        public string PaginationToken { get; private set; }
        
        public bool QueryComplete { get; private set; }

        public OutstandingMessagesQueryResult(IEnumerable<MessageItem> messages, int shardNumber, string paginationToken, bool queryComplete)
        {
            Messages = messages;
            ShardNumber = shardNumber;
            PaginationToken = paginationToken;
            QueryComplete = queryComplete;
        }
    }
}
