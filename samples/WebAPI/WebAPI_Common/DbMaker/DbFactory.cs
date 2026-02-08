using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;

namespace DbMaker;

public static class DbFactory
{
    public static void CreateEntityStore<T>(IAmazonDynamoDB client)
    {
        var tableRequestFactory = new DynamoDbTableFactory();
        var dbTableBuilder = new DynamoDbTableBuilder(client);
    
        CreateTableRequest tableRequest = tableRequestFactory.GenerateCreateTableRequest<T>(
            new DynamoDbCreateProvisionedThroughput
            (
                new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 }
            )
        );
    
        var entityTableName = tableRequest.TableName;
        (bool exist, IEnumerable<string> tables) hasTables = dbTableBuilder.HasTables([entityTableName]).Result;
        if (!hasTables.exist)
        {
            var buildTable = dbTableBuilder.Build(tableRequest).Result;
            dbTableBuilder.EnsureTablesReady([tableRequest.TableName], TableStatus.ACTIVE).Wait();
        }
    }    
}
