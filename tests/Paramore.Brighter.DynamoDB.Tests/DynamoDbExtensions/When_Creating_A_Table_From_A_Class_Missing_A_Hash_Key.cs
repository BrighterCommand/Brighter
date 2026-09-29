using System;
using Amazon.DynamoDBv2.DataModel;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;
using System.Threading.Tasks;
namespace Paramore.Brighter.DynamoDB.Tests.DynamoDbExtensions
{
    public class DynanmoDbMissingHashKeyTests
    {
        [Test]
        public async Task When_Creating_A_Table_From_A_Class_Missing_A_Hash_Key()
        {
             //arrange, act, assert
            await Assert.That(() =>
            {
                var tableRequestFactory = new DynamoDbTableFactory();
                tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(new DynamoDbCreateProvisionedThroughput());
            }).ThrowsExactly<InvalidOperationException>();
            return;
        }

        [DynamoDBTable("DnyamoDbEntity")]
        private sealed class DynamoDbEntity;
    }
}
