namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class TopicQueryContext
    {
        public int NextPage { get; private set; }
        public string LastEvaluatedKey { get; private set; }

        public TopicQueryContext(int nextPage, string lastEvaluatedKey)
        {
            NextPage = nextPage;
            LastEvaluatedKey = lastEvaluatedKey;
        }
    }
}
