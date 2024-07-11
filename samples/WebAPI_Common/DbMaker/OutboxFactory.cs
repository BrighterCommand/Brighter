using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.DynamoDB;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.Sqlite;

namespace DbMaker;

public static class OutboxFactory
{
    public static (IAmAnOutbox, Type, Type) MakeOutbox(
        DatabaseType databaseType,
        RelationalDatabaseConfiguration configuration,
        IServiceCollection services)
    {
        (IAmAnOutbox, Type, Type) outbox = databaseType switch
        {
            DatabaseType.MySql => MakeMySqlOutbox(configuration),
            DatabaseType.MsSql => MakeMsSqlOutbox(configuration),
            DatabaseType.Postgres => MakePostgresSqlOutbox(configuration),
            DatabaseType.Sqlite => MakeSqliteOutBox(configuration),
            _ => throw new InvalidOperationException("Unknown Db type for Outbox configuration")
        };

        return outbox;
    }

    public static void CreateOutbox(IAmazonDynamoDB client, IServiceCollection services)
    {
        var tableRequestFactory = new DynamoDbTableFactory();
        var dbTableBuilder = new DynamoDbTableBuilder(client);
            
        var createTableRequest = new DynamoDbTableFactory().GenerateCreateTableRequest<MessageItem>(
            new DynamoDbCreateProvisionedThroughput(
                new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10},
                new Dictionary<string, ProvisionedThroughput>
                {
                    {"Outstanding", new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}},
                    {"Delivered", new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10}}
                }
            ));
        var outboxTableName = createTableRequest.TableName;
        (bool exist, IEnumerable<string> tables) hasTables = dbTableBuilder.HasTables(new string[] {outboxTableName}).Result;
        if (!hasTables.exist)
        {
            _ = dbTableBuilder.Build(createTableRequest).Result;
            dbTableBuilder.EnsureTablesReady(new[] {createTableRequest.TableName}, TableStatus.ACTIVE).Wait();
        }
    }

    private static (IAmAnOutbox, Type, Type) MakePostgresSqlOutbox(
        RelationalDatabaseConfiguration configuration)
    {
        return (new PostgreSqlOutbox(configuration), typeof(PostgreSqlConnectionProvider),
            typeof(PostgreSqlUnitOfWork));
    }

    private static (IAmAnOutbox, Type, Type) MakeMsSqlOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new ValueTuple<IAmAnOutbox, Type, Type>(new MsSqlOutbox(configuration), typeof(MsSqlConnectionProvider),
            typeof(MsSqlUnitOfWork));
    }

    private static (IAmAnOutbox, Type, Type) MakeMySqlOutbox(RelationalDatabaseConfiguration configuration)
    {
        return (new MySqlOutbox(configuration), typeof(MySqlConnectionProvider), typeof(MySqlUnitOfWork));
    }

    private static (IAmAnOutbox, Type, Type) MakeSqliteOutBox(RelationalDatabaseConfiguration configuration)
    {
        return (new SqliteOutbox(configuration), typeof(SqliteConnectionProvider), typeof(SqliteUnitOfWork));
    }
}
