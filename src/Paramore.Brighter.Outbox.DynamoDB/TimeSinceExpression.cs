using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class TimeSinceExpression
    {
        private Expression _expression;

        public TimeSinceExpression()
        {
            _expression = new Expression();
            _expression.ExpressionStatement = "DeliveryTime >= :v_SinceTime";
        }

        public Expression Generate(DateTime sinceTime)
        {
            var values = new Dictionary<string, DynamoDBEntry>();
            values.Add(":v_SinceTime", sinceTime.Ticks);

            _expression.ExpressionAttributeValues = values;

            return _expression;
        }
    }
}
