using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB.V4
{
    internal sealed class KeyTopicOutstandingCreatedTimeExpression : TopicQueryKeyExpression
    {
        public KeyTopicOutstandingCreatedTimeExpression()
        {
            Expression.ExpressionStatement = "TopicShard = :v_TopicShard and OutstandingCreatedTime < :v_OutstandingCreatedTime";
        }

        public override Expression Generate(string topicName, DateTimeOffset createdTime, int shard)
        {
            Expression.ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>(capacity: 2)
            {
                { ":v_TopicShard", $"{topicName}_{shard}" },
                { ":v_OutstandingCreatedTime", createdTime.Ticks }
            };

            return Expression;
        }
    }
}
