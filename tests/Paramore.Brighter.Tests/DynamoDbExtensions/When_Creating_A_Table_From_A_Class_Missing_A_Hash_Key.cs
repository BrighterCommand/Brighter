using System;
using Amazon.DynamoDBv2.DataModel;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.Tests.DynamoDbExtensions
{
    public class DynanmoDbnMissingHashKeyTests 
    {
        [Fact]
        public void When_Creating_A_Table_From_A_Class_Missing_A_Hash_Key()
        {
             //arrange, act, assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                var tableRequestFactory = new DynamoDbTableFactory();
                tableRequestFactory.GenerateCreateTableMapper<DynamoDbEntity>();
            });
            
        }
        
        
        [DynamoDBTable("DnyamoDbEntity")]
        private class DynamoDbEntity{}
 
    }
}
