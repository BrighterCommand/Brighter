using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB.V4
{
    internal sealed class KeyTopicDeliveredTimeExpression : TopicQueryKeyExpression
    {
        public KeyTopicDeliveredTimeExpression()
        {
            Expression.ExpressionStatement = "TopicShard = :v_TopicShard and DeliveryTime < :v_SinceTime";
        }

        public override Expression Generate(string topicName, DateTimeOffset sinceTime, int shard)
        {
            Expression.ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>(capacity: 2)
            {
                { ":v_TopicShard", $"{topicName}_{shard}" },
                { ":v_SinceTime", sinceTime.Ticks }
            };

            return Expression;
        }
    }
}
