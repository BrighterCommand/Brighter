using System;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal abstract class TopicQueryKeyExpression
    {
        internal Expression Expression { get; } = new();

        public override string ToString()
        {
            return Expression.ExpressionStatement;
        }

        public abstract Expression Generate(string topicName, DateTimeOffset sinceTime, int shard);
    }
}
