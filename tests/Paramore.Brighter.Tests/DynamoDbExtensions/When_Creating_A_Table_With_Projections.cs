using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb.Extensions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.Tests.DynamoDbExtensions
{
    public class DynamoDbFactoryProjectionsTests 
    {
        [Fact]
        public void When_Creating_A_Table_With_Projections()
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
            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableMapper<DynamoDbEntity>(
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
            Assert.Equal(ProjectionType.KEYS_ONLY, tableRequest.GlobalSecondaryIndexes.First(gsi => gsi.IndexName == "GlobalSecondaryIndex").Projection.ProjectionType);
            Assert.Equal(
                new List<string>{"Id", "Version"}, 
                tableRequest.GlobalSecondaryIndexes.First(gsi => gsi.IndexName == "GlobalSecondaryIndex").Projection.NonKeyAttributes);
       
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
