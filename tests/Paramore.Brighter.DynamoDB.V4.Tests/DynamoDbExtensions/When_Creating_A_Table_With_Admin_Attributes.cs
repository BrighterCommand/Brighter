using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.V4.Tests.DynamoDbExtensions;

public class DynamoDbFactoryOtherTableAttributesTests
{
    [Test]
    public async Task When_Creating_A_Table_With_Admin_Attributes()
    {
        //arrange
        var tableRequestFactory = new DynamoDbTableFactory();

        //act
        CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(
            new DynamoDbCreateProvisionedThroughput(),
            billingMode: new BillingMode(BillingMode.PAY_PER_REQUEST),
            sseSpecification: new SSESpecification {Enabled = false},
            streamSpecification: new StreamSpecification {StreamEnabled = false},
            tags: new List<Tag> {new Tag{Key="beta", Value = "True"}, new Tag{Key="paramore", Value = "Brighter"}});

        //assert
        await Assert.That(tableRequest.BillingMode).IsEqualTo(BillingMode.PAY_PER_REQUEST);
        await Assert.That(tableRequest.SSESpecification.Enabled).IsFalse();
        await Assert.That(tableRequest.SSESpecification.Enabled).IsFalse();
        await Assert.That(tableRequest.Tags).Contains(tag => tag.Key == "beta" && tag.Value == "True");
        await Assert.That(tableRequest.Tags).Contains(tag => tag.Key == "paramore" && tag.Value == "Brighter");
    }

    [DynamoDBTable("MyEntity")]
    private sealed class DynamoDbEntity
    {
        [DynamoDBHashKey]
        public string Id { get; set; }
    }
}