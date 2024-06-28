namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class DispatchedTopicQueryContext
    {
        public int NextPage { get; private set; }
        public string LastEvaluatedKey { get; private set; }

        public DispatchedTopicQueryContext(int nextPage, string lastEvaluatedKey)
        {
            NextPage = nextPage;
            LastEvaluatedKey = lastEvaluatedKey;
        }
    }
}
