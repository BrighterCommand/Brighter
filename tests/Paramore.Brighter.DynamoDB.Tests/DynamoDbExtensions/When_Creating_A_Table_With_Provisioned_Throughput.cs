using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb.Extensions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.DynamoDbExtensions
{
    public class DynamoDbDFactoryProvisionedThroughputTests
    {
        [Fact]
        public void When_Creating_A_Table_With_Provisioned_Throughput()
        {
            //arrange
            var tableRequestFactory = new DynamoDbTableFactory();
            var provisonedThroughput = new DynamoDbCreateProvisionedThroughput
            (
                table: new ProvisionedThroughput() {ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                gsiThroughputs: new Dictionary<string, ProvisionedThroughput>() 
                    {
                        { "GlobalSecondaryIndex", new ProvisionedThroughput(readCapacityUnits: 11,writeCapacityUnits: 11) }
                    }
           );
            
            //act
            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableMapper<DynamoDbEntity>(provisonedThroughput);
            
            //assert
            Assert.Equal(10, tableRequest.ProvisionedThroughput.ReadCapacityUnits);
            Assert.Equal(10, tableRequest.ProvisionedThroughput.WriteCapacityUnits);
            Assert.Equal(11,tableRequest.GlobalSecondaryIndexes.First(gsi => gsi.IndexName == "GlobalSecondaryIndex").ProvisionedThroughput.ReadCapacityUnits);
            Assert.Equal(11,tableRequest.GlobalSecondaryIndexes.First(gsi => gsi.IndexName == "GlobalSecondaryIndex").ProvisionedThroughput.WriteCapacityUnits);
    
        }
    
        [DynamoDBTable("MyEntity")]
        private class DynamoDbEntity
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
