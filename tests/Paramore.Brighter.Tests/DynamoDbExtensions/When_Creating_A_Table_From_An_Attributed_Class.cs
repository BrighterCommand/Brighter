using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.Tests.DynamoDbExtensions
{
    public class DynamboDbFactoryGenerateCreateRequestTests
    {
        [Fact]
        public void When_Creating_A_Table_From_An_Attributed_Class()
        {
            //arrange
            var tableRequestFactory = new DynamoDbTableFactory();
            
            //act
            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableMapper<DynamoDbEntity>();
            
            //assert
            Assert.Equal("MyEntity", tableRequest.TableName);
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "StringProperty" && attr.AttributeType == ScalarAttributeType.S);
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "NumberProperty" && attr.AttributeType == ScalarAttributeType.N);
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "ByteArrayProperty" && attr.AttributeType == ScalarAttributeType.B);
            Assert.DoesNotContain(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "UnmarkedProperty");
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "MappedName" && attr.AttributeType == ScalarAttributeType.S);
            Assert.DoesNotContain(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "IgnoredProperty");
            Assert.Contains(tableRequest.KeySchema, kse => kse.AttributeName == "Id" && kse.KeyType == KeyType.HASH);
            Assert.Contains(tableRequest.GlobalSecondaryIndexes,
                gsi => gsi.IndexName == "GlobalSecondaryIndex"
                       && gsi.KeySchema.Any(kse => kse.AttributeName == "GlobalSecondaryId" && kse.KeyType == KeyType.HASH)
                       && gsi.KeySchema.Any(kse => kse.AttributeName == "GlobalSecondaryRangeKey" && kse.KeyType == KeyType.RANGE));
            Assert.Contains(tableRequest.LocalSecondaryIndexes, lsi => lsi.IndexName == "LocalSecondaryIndex" 
                        && lsi.KeySchema.Any(kse => kse.AttributeName == "LocalSecondaryRangeKey" && kse.KeyType == KeyType.RANGE));

        }

        //Required
        [DynamoDBTable("MyEntity")]
        private class DynamoDbEntity
        {
            //Required
           [DynamoDBProperty]
            public string StringProperty { get; set; }

            [DynamoDBProperty]
            public int NumberProperty { get; set; }
            
            [DynamoDBProperty]
            public byte[] ByteArrayProperty { get; set; }
            
            //We only issue create table statements for explicitly marked fields; others can still be persisted though
            public string UnMarkedProperty { get; set; }
           
            [DynamoDBProperty("MappedName")]
            public string RenamedProperty { get; set; }
            
            [DynamoDBIgnore]
            public string IgnoredProperty { get; set; }
            
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
            
            [DynamoDBLocalSecondaryIndexRangeKey(indexName:"LocalSecondaryIndex")]
            public string LocalSecondaryRangeKey { get; set; }

           
       }
    }
}
