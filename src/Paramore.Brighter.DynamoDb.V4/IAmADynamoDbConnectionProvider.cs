using Amazon.DynamoDBv2;

namespace Paramore.Brighter.DynamoDb.V4;

/// <summary>
/// Provides the dynamo db connection we are using, base of any unit of work
/// </summary>
public interface IAmADynamoDbConnectionProvider
{
    /// <summary>
    /// The AWS client for dynamoDb
    /// </summary>
    IAmazonDynamoDB DynamoDb { get; }
}