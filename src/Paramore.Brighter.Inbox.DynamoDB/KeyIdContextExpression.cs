using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Inbox.DynamoDB
{
    internal class KeyIdContextExpression
    {
        private Expression _expression;

        public KeyIdContextExpression()
        {
            _expression = new Expression();
            _expression.ExpressionStatement = "CommandId == :v_CommandId and ContextKey == :v_ContextKey";
        }

        public Expression Generate(Guid id, string contextKey)
        {
            var values = new Dictionary<string, DynamoDBEntry>();
            values.Add(":v_CommandId", id);
            values.Add(":v_ContextKey", contextKey);

            _expression.ExpressionAttributeValues = values;

            return _expression;
  
        }

    }
}
