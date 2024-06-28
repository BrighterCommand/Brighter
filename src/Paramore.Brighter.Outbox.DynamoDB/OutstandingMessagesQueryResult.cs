using System.Collections.Generic;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class OutstandingMessagesQueryResult
    {
        private int _maxShards;

        public IEnumerable<MessageItem> Messages { get; private set; }
        public int ShardNumber { get; private set; }
        public string PaginationToken { get; private set; }
        
        public bool QueryComplete 
        { 
            get
            {
                return PaginationToken != null || ShardNumber < _maxShards;
            }
        }

        public OutstandingMessagesQueryResult(IEnumerable<MessageItem> messages, int shardNumber, int maxShards, string paginationToken)
        {
            _maxShards = maxShards;

            Messages = messages;
            ShardNumber = shardNumber;
            PaginationToken = paginationToken;
        }
    }
}
