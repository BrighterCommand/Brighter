using System.Collections.Generic;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class DispatchedAllTopicsQueryContext
    {
        public int NextPage { get; private set; }
        public string LastEvaluatedKey { get; private set; }
        public List<string> RemainingTopics { get; private set; }

        public DispatchedAllTopicsQueryContext(int nextPage, string lastEvaluatedKey, List<string> remainingTopics)
        {
            NextPage = nextPage;
            LastEvaluatedKey = lastEvaluatedKey;
            RemainingTopics = remainingTopics;
        }   
    }
}
