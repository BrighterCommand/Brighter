using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDB.Tests.TestDoubles;

[DynamoDBTable("MyTransactedEntity")]
public class MyEntity
{
    //Required
    [DynamoDBHashKey]
    public string Id { get; set; }
    [DynamoDBProperty]
    public string Value { get; set; }
    [DynamoDBVersion]
    public int? Version { get; set; }
}
