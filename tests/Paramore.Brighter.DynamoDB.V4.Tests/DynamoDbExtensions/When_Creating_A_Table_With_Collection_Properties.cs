using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.V4.Tests.DynamoDbExtensions;

public class DynamoDbCollectionProperties
{
    [Test]
    public async Task When_Creating_A_Table_With_Collection_Properties()
    {
        //arrange
        var tableRequestFactory = new DynamoDbTableFactory();

        //act
        CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(
            new DynamoDbCreateProvisionedThroughput
            (
                new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}
            )
        );

        //assert
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "Id" && attr.AttributeType == ScalarAttributeType.S)).IsTrue();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "StringArray" && attr.AttributeType.Value == "SS")).IsTrue();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "IntArray" && attr.AttributeType.Value == "NS")).IsTrue();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "DoubleArray" && attr.AttributeType.Value == "NS")).IsTrue();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "GenericList" && attr.AttributeType.Value == "L")).IsTrue();
        await Assert.That((tableRequest.AttributeDefinitions).Any(attr => attr.AttributeName == "GenericMap" && attr.AttributeType.Value == "M")).IsTrue();
    }

    [DynamoDBTable("MyEntity")]
    private sealed class DynamoDbEntity
    {
        [DynamoDBHashKey]
        [DynamoDBProperty]
        public string Id { get; set; }

        [DynamoDBProperty]
        public string[] StringArray { get; set; }

        [DynamoDBProperty]
        public int[] IntArray { get; set; }

        [DynamoDBProperty]
        public double[] DoubleArray { get; set; }

        [DynamoDBProperty]
        public List<string> GenericList { get; set; }

        [DynamoDBProperty]
        public Dictionary<string, object> GenericMap{ get; set; }
    }
}
