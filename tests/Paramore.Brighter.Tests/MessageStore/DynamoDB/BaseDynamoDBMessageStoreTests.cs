using System;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.MessageStore.DynamoDB;

namespace Paramore.Brighter.Tests.MessageStore.DynamoDB
{
    public class BaseDynamoDBMessageStoreTests : IDisposable
    {
        protected readonly ProvisionedThroughput _throughput = new ProvisionedThroughput(2, 1);
        protected readonly DynamoDbTestHelper _dynamoDbTestHelper;
        protected readonly DynamoDbMessageStore _dynamoDbMessageStore;

        public BaseDynamoDBMessageStoreTests()
        {           
            _dynamoDbTestHelper = new DynamoDbTestHelper();

            var createTableRequest = new DynamoDbMessageStoreBuilder(_dynamoDbTestHelper.DynamoDbMessageStoreTestConfiguration.TableName)
                .CreateMessageStoreTableRequest(_throughput, _throughput);

            _dynamoDbTestHelper.CreateMessageStoreTable(createTableRequest);

            _dynamoDbMessageStore = new DynamoDbMessageStore(_dynamoDbTestHelper.DynamoDbContext, _dynamoDbTestHelper.DynamoDbMessageStoreTestConfiguration);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Release();
        }

        private void Release()
        {
            _dynamoDbTestHelper.CleanUpMessageDb();
        }

        ~BaseDynamoDBMessageStoreTests()
        {
            Release();
        }
    }
}
