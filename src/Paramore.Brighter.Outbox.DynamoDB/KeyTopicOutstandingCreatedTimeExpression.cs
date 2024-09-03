using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class KeyTopicOutstandingCreatedTimeExpression
    {
        private readonly Expression _expression;

        public KeyTopicOutstandingCreatedTimeExpression()
        {
            _expression = new Expression { ExpressionStatement = "TopicShard = :v_TopicShard and OutstandingCreatedTime < :v_OutstandingCreatedTime" };
        }

        public override string ToString()
        {
            return _expression.ExpressionStatement;
        }

        public Expression Generate(string topicName, DateTime createdTime, int shard)
        {
            var values = new Dictionary<string, DynamoDBEntry>();
            values.Add(":v_TopicShard", $"{topicName}_{shard}");
            values.Add(":v_OutstandingCreatedTime", createdTime.Ticks);

            _expression.ExpressionAttributeValues = values;

            return _expression;
        }
    }
}
