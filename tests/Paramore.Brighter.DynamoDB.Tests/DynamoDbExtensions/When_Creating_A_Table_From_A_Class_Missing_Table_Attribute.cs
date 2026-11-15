using System;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;
using System.Threading.Tasks;
namespace Paramore.Brighter.DynamoDB.Tests.DynamoDbExtensions
{
    public class DynamoDbFactoryMissingTableAttributeTests
    {
        [Test]
        public async Task When_Creating_A_Table_From_A_Class_Missing_Table_Attribute()
        {
            //arrange, act, assert
            await Assert.That(() =>
            {
                var tableRequestFactory = new DynamoDbTableFactory();
                tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(new DynamoDbCreateProvisionedThroughput());
            }).ThrowsExactly<InvalidOperationException>();
            return;
        }

        private sealed class DynamoDbEntity;
    }
}
