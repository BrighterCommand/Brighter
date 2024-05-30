using System.Collections.Generic;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class AllTopicsQueryContext
    {
        public int NextPage { get; private set; }
        public string LastEvaluatedKey { get; private set; }
        public List<string> OutstandingTopics { get; private set; }

        public AllTopicsQueryContext(int nextPage, string lastEvaluatedKey, List<string> outstandingTopics)
        {
            NextPage = nextPage;
            LastEvaluatedKey = lastEvaluatedKey;
            OutstandingTopics = outstandingTopics;
        }   
    }
}
