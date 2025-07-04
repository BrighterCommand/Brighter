using Amazon.DynamoDBv2.DataModel;

namespace Paramore.Brighter.DynamoDB.V4.Tests.TestDoubles;

[DynamoDBTable("MyTransactedEntity")]
public class MyEntity
{
    //Required
    [DynamoDBHashKey]
    [DynamoDBProperty]
    public string Id { get; set; }
    [DynamoDBProperty]
    public string Value { get; set; }
    [DynamoDBVersion]
    public int? Version { get; set; }
}
