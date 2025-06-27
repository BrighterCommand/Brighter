using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DocumentModel;

namespace Paramore.Brighter.Outbox.DynamoDB.V4;

internal sealed class KeyTopicOutstandingCreatedTimeExpression
{
    private readonly Expression _expression = new()
    {
        ExpressionStatement = "TopicShard = :v_TopicShard and OutstandingCreatedTime < :v_OutstandingCreatedTime"
    };

    public override string ToString()
    {
        return _expression.ExpressionStatement;
    }

    public Expression Generate(string topicName, DateTimeOffset createdTime, int shard)
    {
        _expression.ExpressionAttributeValues = new Dictionary<string, DynamoDBEntry>(capacity: 2)
        {
            { ":v_TopicShard", $"{topicName}_{shard}" },
            { ":v_OutstandingCreatedTime", createdTime.Ticks }
        };

        return _expression;
    }
}