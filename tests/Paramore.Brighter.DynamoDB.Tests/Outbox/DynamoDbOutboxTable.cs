using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

public static class DynamoDbOutboxTable
{
    private static readonly SemaphoreSlim s_semaphoreSlim = new(1, 1);
    private static string? s_tableName;

    public static async Task<string> EnsureTableIsCreatedAsync(AmazonDynamoDBClient dynamoDbClient)
    {
        if (!string.IsNullOrEmpty(s_tableName))
        {
            return s_tableName;
        }

        var tableName = string.Empty;
        await s_semaphoreSlim.WaitAsync();
        try
        {
            if (!string.IsNullOrEmpty(s_tableName))
            {
                return s_tableName;
            }

            var request = BuildCreateTableRequest();

            tableName = request.TableName;
            var builder = new DynamoDbTableBuilder(dynamoDbClient);
            await builder.Build(request);

            await builder.EnsureTablesReady([request.TableName], TableStatus.ACTIVE);

            s_tableName = request.TableName;
            return s_tableName;
        }
        catch (ResourceInUseException)
        {
            // just in case the table already exists
            return s_tableName = tableName;
        }
        finally
        {
            s_semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Creates a table of its own for one case of the overall outstanding count, so the case counts only what it adds.
    /// </summary>
    public static async Task<string> CreateTableAsync(AmazonDynamoDBClient dynamoDbClient)
    {
        var request = BuildCreateTableRequest();
        request.TableName = $"{request.TableName}_{Uuid.New():N}";

        var builder = new DynamoDbTableBuilder(dynamoDbClient);
        await builder.Build(request);
        await builder.EnsureTablesReady([request.TableName], TableStatus.ACTIVE);

        return request.TableName;
    }

    public static async Task DeleteTableAsync(AmazonDynamoDBClient dynamoDbClient, string tableName)
    {
        var builder = new DynamoDbTableBuilder(dynamoDbClient);
        await builder.Delete([tableName]);
        await builder.EnsureTablesDeleted([tableName]);
    }

    private static CreateTableRequest BuildCreateTableRequest()
    {
        return new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                new Dictionary<string, ProvisionedThroughput?>
                {
                    ["Outstanding"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["OutstandingAllTopics"] = new()
                    {
                        ReadCapacityUnits = 10,
                        WriteCapacityUnits = 10,
                    },
                    ["Delivered"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                    ["DeliveredAllTopics"] = new()
                    {
                        ReadCapacityUnits = 10,
                        WriteCapacityUnits = 10,
                    },
                    ["Causation"] = new() { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                }
            )
        );
    }
}
