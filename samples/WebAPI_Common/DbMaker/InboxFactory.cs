using Paramore.Brighter;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.Inbox.Sqlite;

namespace DbMaker;

public static class InboxFactory
{
    public static IAmAnInbox MakeInbox(DatabaseType databaseType, IAmARelationalDatabaseConfiguration configuration)
    {
        return databaseType switch
        {
            DatabaseType.Sqlite => new SqliteInbox(configuration),
            DatabaseType.MySql => new MySqlInbox(configuration),
            DatabaseType.MsSql => new MsSqlInbox(configuration),
            DatabaseType.Postgres => new PostgreSqlInbox(configuration),
            _ => throw new ArgumentOutOfRangeException(nameof(databaseType), "Database type is not supported")
        };
    }

       public static void CreateInbox<T>(IAmazonDynamoDB client, IServiceCollection services) where T : class, IRequest
    {
        var tableRequestFactory = new DynamoDbTableFactory();
        var dbTableBuilder = new DynamoDbTableBuilder(client);

        var createTableRequest = new DynamoDbTableFactory().GenerateCreateTableRequest<CommandItem<T>>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput { ReadCapacityUnits = 10, WriteCapacityUnits = 10 },
                new Dictionary<string, ProvisionedThroughput>()
            ));

        var tableName = createTableRequest.TableName;
        (bool exist, IEnumerable<string> tables) hasTables = dbTableBuilder.HasTables(new string[] { tableName }).Result;
        if (!hasTables.exist)
        {
            var buildTable = dbTableBuilder.Build(createTableRequest).Result;
            dbTableBuilder.EnsureTablesReady(new[] { createTableRequest.TableName }, TableStatus.ACTIVE).Wait();
        }
    }
}
