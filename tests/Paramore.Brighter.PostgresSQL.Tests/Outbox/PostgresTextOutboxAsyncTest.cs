using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

public class PostgresTextOutboxAsyncTest : RelationDatabaseOutboxAsyncTest
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new PostgreSqlOutbox(configuration);
    }

    protected override async Task CreateOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new NpgsqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = PostgreSqlOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new NpgsqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    protected override IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new PostgreSqlTransactionProvider(Configuration);
    }
}
