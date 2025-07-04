using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.DynamoDb.V4;
using Paramore.Brighter.Locking.DynamoDB.V4;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Locking;

public class DynamoDBLockingBaseTest
{
    protected DynamoDbTableBuilder DbTableBuilder { get; }
    protected string LockTableName { get; }
    protected AWSCredentials Credentials { get; set; }
    protected IAmazonDynamoDB Client { get; }
    private DynamoDBContext _dynamoDBContext;

    protected DynamoDBLockingBaseTest()
    {
        Client = CreateClient();
        _dynamoDBContext = new DynamoDBContext(Client);
        DbTableBuilder = new DynamoDbTableBuilder(Client);

        // Create the lock table
        var createTableRequest = new DynamoDbTableFactory().GenerateCreateTableRequest<LockItem>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 }
            ));
        LockTableName = createTableRequest.TableName;

        (bool exist, IEnumerable<string> _) hasTables = DbTableBuilder.HasTables([LockTableName]).GetAwaiter().GetResult();
        if (!hasTables.exist)
        {
            DbTableBuilder.Build(createTableRequest).GetAwaiter().GetResult();
            DbTableBuilder.EnsureTablesReady([createTableRequest.TableName], TableStatus.ACTIVE).GetAwaiter().GetResult();
        }
    }

    protected async Task<LockItem> GetLockItem(string resourceId)
    {
        return await _dynamoDBContext.LoadAsync<LockItem>(resourceId);
    }

    private IAmazonDynamoDB CreateClient()
    {
        Credentials = new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey");

        var clientConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:8000"
        };

        return new AmazonDynamoDBClient(Credentials, clientConfig);
    }
}