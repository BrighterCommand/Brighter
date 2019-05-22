using System;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.Tests.Outbox.DynamoDB
{
    public class BaseDynamoDBOutboxTests : IDisposable
    {
        protected readonly ProvisionedThroughput _throughput = new ProvisionedThroughput(2, 1);
        protected readonly DynamoDbTestHelper _dynamoDbTestHelper;
        protected readonly DynamoDbOutbox DynamoDbOutbox;

        public BaseDynamoDBOutboxTests()
        {           
            _dynamoDbTestHelper = new DynamoDbTestHelper();

            var createTableRequest = new DynamoDbOutboxBuilder(_dynamoDbTestHelper.DynamoDbOutboxTestConfiguration.TableName)
                .CreateOutboxTableRequest(_throughput, _throughput);

            _dynamoDbTestHelper.CreateOutboxTable(createTableRequest);

            DynamoDbOutbox = new DynamoDbOutbox(_dynamoDbTestHelper.DynamoDbContext, _dynamoDbTestHelper.DynamoDbOutboxTestConfiguration);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Release();
        }

        private void Release()
        {
            _dynamoDbTestHelper.CleanUpOutboxDb();
        }

        ~BaseDynamoDBOutboxTests()
        {
            Release();
        }
    }
}
