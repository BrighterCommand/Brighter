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
            _expression = new Expression { ExpressionStatement = "Topic = :v_Topic and CreatedTime < :v_CreatedTime" };
        }

        public override string ToString()
        {
            return _expression.ExpressionStatement;
        }

        public Expression Generate(string topicName, DateTime createdTime)
        {
            var values = new Dictionary<string, DynamoDBEntry>();
            values.Add(":v_Topic", topicName);
            values.Add(":v_CreatedTime", createdTime.Ticks);

            _expression.ExpressionAttributeValues = values;

            return _expression;
        }
    }
}
