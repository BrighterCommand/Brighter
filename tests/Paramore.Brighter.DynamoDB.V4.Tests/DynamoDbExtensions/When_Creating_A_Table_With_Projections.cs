using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.V4.Tests.DynamoDbExtensions;

public class DynamoDbFactoryProjectionsTests
{
    [Test]
    public async Task When_Creating_A_Table_With_Projections()
    {
        var tableRequestFactory = new DynamoDbTableFactory();
        var gsiProjection = new DynamoGSIProjections
        (
            projections: new Dictionary<string, Projection>
            {
                {"GlobalSecondaryIndex", new Projection{ ProjectionType = ProjectionType.KEYS_ONLY, NonKeyAttributes = new List<string>{"Id", "Version"}}}
            }
        );

        //act
        CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                new Dictionary<string, ProvisionedThroughput>
                {
                    {
                        "GlobalSecondaryIndex", new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}
                    }
                }
            ),
            gsiProjection
        );

        //assert
        await Assert.That(tableRequest.GlobalSecondaryIndexes.First(gsi => gsi.IndexName == "GlobalSecondaryIndex").Projection.ProjectionType).IsEqualTo(ProjectionType.KEYS_ONLY);
        await Assert.That(tableRequest.GlobalSecondaryIndexes.First(gsi => gsi.IndexName == "GlobalSecondaryIndex").Projection.NonKeyAttributes).IsEquivalentTo(new List<string>{"Id", "Version"});
    }

    [DynamoDBTable("MyEntity")]
    private sealed class DynamoDbEntity
    {
        [DynamoDBHashKey]
        public string Id { get; set; }

        [DynamoDBRangeKey]
        public string RangeKey { get; set; }

        [DynamoDBVersion]
        public int? Version { get; set; }

        [DynamoDBGlobalSecondaryIndexHashKey("GlobalSecondaryIndex")]
        public string GlobalSecondaryId { get; set; }

        [DynamoDBGlobalSecondaryIndexRangeKey("GlobalSecondaryIndex")]
        public string GlobalSecondaryRangeKey { get; set; }
    }
}