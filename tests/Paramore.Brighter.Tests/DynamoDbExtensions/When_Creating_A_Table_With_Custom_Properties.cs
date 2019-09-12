using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb.Extensions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.Tests.DynamoDbExtensions
{
    public class DynamoDbCustomProperties 
    {
        [Fact]
        public void When_Creating_A_Table_With_Custom_Properties()
        {
            //arrange
            var tableRequestFactory = new DynamoDbTableFactory();
            
            //act
            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableMapper<DynamoDbEntity>(
                new DynamoDbCreateProvisionedThroughput
                (
                    new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                    new Dictionary<string, ProvisionedThroughput>
                    {
                        {
                            "GlobalSecondaryIndex", new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10} 
                        }
                    }
                )
            );
            
            //assert
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "Id" && attr.AttributeType == ScalarAttributeType.S);
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "UUID" && attr.AttributeType == ScalarAttributeType.S);
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "UniqueIdentifier" && attr.AttributeType == ScalarAttributeType.S);
         }
        
        [DynamoDBTable("MyEntity")]
        private class DynamoDbEntity
        {
            [DynamoDBHashKey]
            [DynamoDBProperty]
            public string Id { get; set; }
             
            [DynamoDBProperty(typeof(GUIDConverter))]
            public Guid UUID { get; set; }
            
            [DynamoDBProperty("UniqueIdentifier ", typeof(GUIDConverter))]
            public Guid UniqueIdentifier { get; set; }
        }
    }
}
