using System;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.DynamoDbExtensions
{
    public class DynamoDbFactoryMissingTableAttributeTests
    {
        [Test]
        public Task When_Creating_A_Table_From_A_Class_Missing_Table_Attribute()
        {
            //arrange, act, assert
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                var tableRequestFactory = new DynamoDbTableFactory();
                tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(new DynamoDbCreateProvisionedThroughput());
            });
            return Task.CompletedTask;
        }

        private sealed class DynamoDbEntity;
    }
}
