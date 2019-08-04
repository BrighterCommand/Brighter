using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class KeyTopicDeliveredTimeExpression
    {
        private Expression _expression;

        public KeyTopicDeliveredTimeExpression()
        {
            _expression = new Expression();
            _expression.ExpressionStatement = "Topic = :v_Topic and DeliveryTime >= :v_SinceTime";
 
        }

        public override string ToString()
        {
            return _expression.ExpressionStatement;
        }

        public Expression Generate(string topicName, DateTime sinceTime)
        {
            var values = new Dictionary<string, DynamoDBEntry>();
            values.Add(":v_Topic", topicName);
            values.Add(":v_SinceTime", sinceTime.Ticks);

            _expression.ExpressionAttributeValues = values;

            return _expression;
        }
    }
}
