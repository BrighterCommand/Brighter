using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB.V4;

public class NoDispatchTimeExpression
{
        
    private Expression _expression;

    public NoDispatchTimeExpression()
    {
        _expression = new Expression();
        _expression.ExpressionStatement = "DeliveryTime = :null";
        var values = new Dictionary<string, DynamoDBEntry>();
        values.Add(":null", 0L);
        _expression.ExpressionAttributeValues = values;
    }


    public Expression Generate()
    {
        return _expression;
    }
}