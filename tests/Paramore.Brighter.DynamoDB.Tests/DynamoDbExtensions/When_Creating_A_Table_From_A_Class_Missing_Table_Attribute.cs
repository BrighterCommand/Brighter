using System;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.DynamoDbExtensions
{
    public class DynamoDbFactoryMissingTableAttributeTests
    {
        [Fact]
        public void When_Creating_A_Table_From_A_Class_Missing_Table_Attribute()
        {
            //arrange, act, assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                var tableRequestFactory = new DynamoDbTableFactory();
                tableRequestFactory.GenerateCreateTableRequest<DynamoDbEntity>(new DynamoDbCreateProvisionedThroughput());
            });
        }

        private sealed class DynamoDbEntity;
    }
}
