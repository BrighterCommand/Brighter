using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Inbox.DynamoDB.V4;

internal sealed class KeyIdContextExpression
{
    private readonly Expression _expression = new()
    {
        ExpressionStatement = "CommandId = :v_CommandId and ContextKey = :v_ContextKey"
    };

    public Expression Generate(string id, string contextKey)
    {
        _expression.ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>(capacity: 2)
        {
            { ":v_CommandId", id },
            { ":v_ContextKey", contextKey }
        };

        return _expression;
    }
}
