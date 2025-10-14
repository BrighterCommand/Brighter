using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.Sqlite;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class SqliteTextOutboxAsyncTest : RelationDatabaseOutboxAsyncTest
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new SqliteOutbox(configuration);
    }

    protected override async Task CreateOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SqliteOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new SqliteConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }

    protected override IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new SqliteTransactionProvider(Configuration);
    }
}
