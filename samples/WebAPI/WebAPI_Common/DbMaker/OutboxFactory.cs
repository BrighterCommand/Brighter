using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MsSql.EntityFrameworkCore;
using Paramore.Brighter.MySql;
using Paramore.Brighter.MySql.EntityFrameworkCore;
using Paramore.Brighter.Outbox.DynamoDB;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.PostgreSql.EntityFrameworkCore;
using Paramore.Brighter.Sqlite;
using Paramore.Brighter.Sqlite.EntityFrameworkCore;

namespace DbMaker;

public static class OutboxFactory
{
    public static (IAmAnOutbox, Type, Type) MakeDapperOutbox(Rdbms rdbms, RelationalDatabaseConfiguration configuration)
    {
        (IAmAnOutbox, Type, Type) outbox = rdbms switch
        {
            Rdbms.MySql => MakeDapperMySqlOutbox(configuration),
            Rdbms.MsSql => MakeDapperMsSqlOutbox(configuration),
            Rdbms.Postgres => MakeDapperPostgresSqlOutbox(configuration),
            Rdbms.Sqlite => MakeDapperSqliteOutBox(configuration),
            _ => throw new InvalidOperationException("Unknown Db type for Outbox configuration")
        };

        return outbox;
    }

    public static void MakeDynamoOutbox(IAmazonDynamoDB client)
    {
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
        (bool exist, IEnumerable<string> tables) hasTables = dbTableBuilder.HasTables(new[] {outboxTableName}).Result;
        if (!hasTables.exist)
        {
            _ = dbTableBuilder.Build(createTableRequest).Result;
            dbTableBuilder.EnsureTablesReady(new[] {createTableRequest.TableName}, TableStatus.ACTIVE).Wait();
        }
    }
    
    public static (IAmAnOutbox, Type, Type) MakeEfOutbox<T>(Rdbms rdbms,  RelationalDatabaseConfiguration configuration)
        where T : DbContext
    {
        (IAmAnOutbox, Type, Type) outbox = rdbms switch
        {
            Rdbms.MySql => MakeEfMySqlOutbox<T>(configuration),
            Rdbms.MsSql => MakeEfMsSqlOutbox<T>(configuration),
            Rdbms.Postgres => MakeEfPostgreSqlOutbox<T>(configuration),
            Rdbms.Sqlite => MakeEfSqliteOutBox<T>(configuration),
            _ => throw new InvalidOperationException("Unknown Db type for Outbox configuration")
        };

        return outbox;
    }

    private static (IAmAnOutbox, Type, Type) MakeDapperPostgresSqlOutbox(RelationalDatabaseConfiguration configuration)
    {
        return (new PostgreSqlOutbox(configuration), typeof(PostgreSqlConnectionProvider),
            typeof(PostgreSqlTransactionProvider));
    }
    
    private static (IAmAnOutbox, Type, Type) MakeEfPostgreSqlOutbox<T>(RelationalDatabaseConfiguration configuration)
        where T : DbContext
    {
        return (new PostgreSqlOutbox(configuration), typeof(PostgreSqlEntityFrameworkTransactionProvider<T>), 
        typeof(PostgreSqlConnectionProvider));
    }

    private static (IAmAnOutbox, Type, Type) MakeDapperMsSqlOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new ValueTuple<IAmAnOutbox, Type, Type>(new MsSqlOutbox(configuration), typeof(MsSqlConnectionProvider),
            typeof(MsSqlTransactionProvider));
    }
    
    private static (IAmAnOutbox, Type, Type) MakeEfMsSqlOutbox<T>(RelationalDatabaseConfiguration configuration)
        where T : DbContext
    {
        return new ValueTuple<IAmAnOutbox, Type, Type>(new MsSqlOutbox(configuration), typeof(MsSqlEntityFrameworkCoreTransactionProvider<T>),  
            typeof(MsSqlConnectionProvider));
    }

    private static (IAmAnOutbox, Type, Type) MakeDapperMySqlOutbox(RelationalDatabaseConfiguration configuration)
    {
        return (new MySqlOutbox(configuration), typeof(MySqlConnectionProvider), typeof(MySqlTransactionProvider));
    }
    
    private static (IAmAnOutbox, Type, Type) MakeEfMySqlOutbox<T>(RelationalDatabaseConfiguration configuration)
        where T : DbContext
    {
        return (new MySqlOutbox(configuration),typeof(MySqlEntityFrameworkTransactionProvider<T>), typeof(MySqlConnectionProvider));
    }

    private static (IAmAnOutbox, Type, Type) MakeDapperSqliteOutBox(RelationalDatabaseConfiguration configuration)
    {
        return (new SqliteOutbox(configuration), typeof(SqliteConnectionProvider), typeof(SqliteTransactionProvider));
    }
    
    private static (IAmAnOutbox, Type, Type) MakeEfSqliteOutBox<T>(RelationalDatabaseConfiguration configuration)
        where T : DbContext
    {
        return (new SqliteOutbox(configuration), typeof(SqliteEntityFrameworkTransactionProvider<T>), typeof(SqliteConnectionProvider));
    }
}
