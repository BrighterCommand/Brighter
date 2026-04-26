using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.DynamoDbExtensions
{
    public class DynamoDbDFactoryProvisionedThroughputTests
    {
        [Test]
        public async Task When_Creating_A_Table_With_Provisioned_Throughput()
        {
            //arrange
            var tableRequestFactory = new DynamoDbTableFactory();
            var provisonedThroughput = new DynamoDbCreateProvisionedThroughput
            (
                table: new ProvisionedThroughput {ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                gsiThroughputs: new Dictionary<string, ProvisionedThroughput>
                {
                    { "GlobalSecondaryIndex", new ProvisionedThroughput(readCapacityUnits: 11, writeCapacityUnits: 11) }
                }
           );

            //act
            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(provisonedThroughput);

            //assert
            await Assert.That(tableRequest.ProvisionedThroughput.ReadCapacityUnits).IsEqualTo(10);
            await Assert.That(tableRequest.ProvisionedThroughput.WriteCapacityUnits).IsEqualTo(10);
            await Assert.That(tableRequest.GlobalSecondaryIndexes.First(gsi => gsi.IndexName == "GlobalSecondaryIndex").ProvisionedThroughput.ReadCapacityUnits).IsEqualTo(11);
            await Assert.That(tableRequest.GlobalSecondaryIndexes.First(gsi => gsi.IndexName == "GlobalSecondaryIndex").ProvisionedThroughput.WriteCapacityUnits).IsEqualTo(11);
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
}
