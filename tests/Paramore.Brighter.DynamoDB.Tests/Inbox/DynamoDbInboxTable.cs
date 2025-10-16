using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.Inbox;

public static class DynamoDbInboxTable
{
    private static readonly SemaphoreSlim s_semaphoreSlim = new SemaphoreSlim(1, 1);
    private static string? s_tableName;

    public static async Task<string> EnsureTableIsCreatedAsync(AmazonDynamoDBClient dynamoDbClient)
    {
        if (!string.IsNullOrEmpty(s_tableName))
        {
            return s_tableName;
        }
        
        await s_semaphoreSlim.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(s_tableName))
            {
                return s_tableName;
            }

            var request = new DynamoDbTableFactory()
                .GenerateCreateTableRequest<CommandItem<MyCommand>>(
                    new DynamoDbCreateProvisionedThroughput(new ProvisionedThroughput
                    {
                        ReadCapacityUnits = 10, WriteCapacityUnits = 10
                    }));

            s_tableName = request.TableName;
            var builder = new DynamoDbTableBuilder(dynamoDbClient);
            await builder.Build(request);

            await builder.EnsureTablesReady([request.TableName], TableStatus.ACTIVE);
            return s_tableName;
        }
        catch (ResourceInUseException)
        {
            // just in case the table already exists
            return s_tableName;
        }
        finally
        {
            s_semaphoreSlim.Release();
        }
    }
}
