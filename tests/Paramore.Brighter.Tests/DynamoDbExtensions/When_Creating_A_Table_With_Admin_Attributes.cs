using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.Tests.DynamoDbExtensions
{
    public class DynamoDbFactoryOtherTableAttributesTests 
    {
        [Fact]
        public void When_Creating_A_Table_With_Admin_Attributes()
        {
            //arrange
            var tableRequestFactory = new DynamoDbTableFactory();
            
            //act
            CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableMapper<DynamoDbEntity>(
                billingMode: new BillingMode(BillingMode.PAY_PER_REQUEST),
                sseSpecification: new SSESpecification {Enabled = false},
                streamSpecification: new StreamSpecification {StreamEnabled = false},
                tags: new List<Tag> {new Tag{Key="beta", Value = "True"}, new Tag{Key="paramore", Value = "Brighter"}});

            //assert
            Assert.Equal(BillingMode.PAY_PER_REQUEST, tableRequest.BillingMode);
            Assert.False(tableRequest.SSESpecification.Enabled);
            Assert.False(tableRequest.SSESpecification.Enabled);
            Assert.Contains(tableRequest.Tags, tag => tag.Key == "beta" && tag.Value == "True");
            Assert.Contains(tableRequest.Tags, tag => tag.Key == "paramore" && tag.Value == "Brighter");
        }
    
        [DynamoDBTable("MyEntity")]
        private class DynamoDbEntity
        {
            [DynamoDBHashKey]
            public string Id { get; set; }
        }
 }
}
