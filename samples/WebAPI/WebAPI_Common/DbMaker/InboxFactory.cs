using Microsoft.Extensions.Logging;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Inbox.DynamoDB;
using Paramore.Brighter.Inbox.DynamoDB.V4;
using Paramore.Brighter.Inbox.MsSql;
using Paramore.Brighter.Inbox.MySql;
using Paramore.Brighter.Inbox.Postgres;
using Paramore.Brighter.Inbox.Sqlite;
using Paramore.Brighter.Outbox.DynamoDB;

namespace DbMaker;

public static class InboxFactory
{
    public static IAmAnInbox MakeInbox(Rdbms rdbms, IAmARelationalDatabaseConfiguration configuration, ILoggerFactory loggerFactory)
    {
        return rdbms switch
        {
            Rdbms.Sqlite => new SqliteInbox(configuration, logger: loggerFactory.CreateLogger<SqliteInbox>()),
            Rdbms.MySql => new MySqlInbox(configuration, logger: loggerFactory.CreateLogger<MySqlInbox>()),
            Rdbms.MsSql => new MsSqlInbox(configuration, logger: loggerFactory.CreateLogger<MsSqlInbox>()),
            Rdbms.Postgres => new PostgreSqlInbox(configuration, logger: loggerFactory.CreateLogger<PostgreSqlInbox>()),
            _ => throw new ArgumentOutOfRangeException(nameof(rdbms), "Database type is not supported")
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
        (bool exist, IEnumerable<string> tables) hasTables = dbTableBuilder.HasTables([tableName]).Result;
        if (!hasTables.exist)
        {
            var buildTable = dbTableBuilder.Build(createTableRequest).Result;
            dbTableBuilder.EnsureTablesReady([createTableRequest.TableName], TableStatus.ACTIVE).Wait();
        }
    }
}
