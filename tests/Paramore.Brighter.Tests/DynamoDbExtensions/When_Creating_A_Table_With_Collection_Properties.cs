using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb.Extensions;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.Tests.DynamoDbExtensions
{
    public class DynamoDbCollectionProperties 
    {
        [Fact]
        public void When_Creating_A_Table_With_Collection_Properties()
        {
            //arrange
            var tableRequestFactory = new DynamoDbTableFactory();
            
            //act
            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableMapper<DynamoDbEntity>(
                new DynamoDbCreateProvisionedThroughput
                (
                    new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}
                )
            );
            
            //assert
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "Id" && attr.AttributeType == ScalarAttributeType.S);
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "StringArray" && attr.AttributeType.Value == "SS");
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "IntArray" && attr.AttributeType.Value == "NS");
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "DoubleArray" && attr.AttributeType.Value == "NS");
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "GenericList" && attr.AttributeType.Value == "L");
            Assert.Contains(tableRequest.AttributeDefinitions, attr => attr.AttributeName == "GenericMap" && attr.AttributeType.Value == "M");
        }
        
        [DynamoDBTable("MyEntity")]
        private class DynamoDbEntity
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
}
