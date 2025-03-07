using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal sealed class TimeSinceExpression
    {
        private readonly Expression _expression = new()
        {
            ExpressionStatement = "DeliveryTime >= :v_SinceTime"
        };

        public Expression Generate(DateTime sinceTime)
        {
            _expression.ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>(capacity: 1)
            {
                { ":v_SinceTime", sinceTime.Ticks }
            };

            return _expression;
        }
    }
}
