using System.Collections.Generic;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class OutstandingAllTopicsQueryContext
    {
        public int NextPage { get; private set; }
        public string LastEvaluatedKey { get; private set; }
        public int ShardNumber { get; private set; }
        public List<string> RemainingTopics { get; private set; }

        public OutstandingAllTopicsQueryContext(int nextPage, string lastEvaluatedKey, int shardNumber, List<string> remainingTopics)
        {
            NextPage = nextPage;
            LastEvaluatedKey = lastEvaluatedKey;
            ShardNumber = shardNumber;
            RemainingTopics = remainingTopics;
        }   
    }
}
