namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class OutstandingTopicQueryContext
    {
        public int NextPage { get; private set; }
        public int ShardNumber { get; private set; }
        public string LastEvaluatedKey { get; private set; }

        public OutstandingTopicQueryContext(int nextPage, int shardNumber, string lastEvaluatedKey)
        {
            NextPage = nextPage;
            ShardNumber = shardNumber;
            LastEvaluatedKey = lastEvaluatedKey;
        }
    }
}
