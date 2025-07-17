using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB.V4;

internal sealed class KeyTopicDeliveredTimeExpression
{
    private readonly Expression _expression = new()
    {
        ExpressionStatement = "Topic = :v_Topic and DeliveryTime < :v_SinceTime"
    };

    public override string ToString()
    {
        return _expression.ExpressionStatement;
    }

    public Expression Generate(string topicName, DateTimeOffset sinceTime)
    {
        _expression.ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>(capacity: 2)
        {
            { ":v_Topic", topicName },
            { ":v_SinceTime", sinceTime.Ticks }
        };

        return _expression;
    }
}