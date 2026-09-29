using System;
using Amazon.DynamoDBv2.DataModel;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.V4.Tests.DynamoDbExtensions;

public class DynanmoDbMissingHashKeyTests
{
    [Test]
    public async Task When_Creating_A_Table_From_A_Class_Missing_A_Hash_Key()
    {
        //arrange, act, assert
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var tableRequestFactory = new DynamoDbTableFactory();
            tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(new DynamoDbCreateProvisionedThroughput());
        });
        return;
    }

    [DynamoDBTable("DnyamoDbEntity")]
    private sealed class DynamoDbEntity;
}
