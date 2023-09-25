using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class KeyTopicCreatedTimeExpression
    {
        private readonly Expression _expression;

        public KeyTopicCreatedTimeExpression()
        {
            _expression = new Expression { ExpressionStatement = "TopicShard = :v_TopicShard and CreatedTime < :v_CreatedTime" };
        }

        public override string ToString()
        {
            return _expression.ExpressionStatement;
        }

        public Expression Generate(string topicName, DateTime createdTime, int shard)
        {
            var values = new Dictionary<string, DynamoDBEntry>();
            values.Add(":v_TopicShard", $"{topicName}_{shard}");
            values.Add(":v_CreatedTime", createdTime.Ticks);

            _expression.ExpressionAttributeValues = values;

            return _expression;
        }
    }
}
