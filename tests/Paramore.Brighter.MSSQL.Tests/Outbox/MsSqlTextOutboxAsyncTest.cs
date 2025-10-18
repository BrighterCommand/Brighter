using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

public class MsSqlTextOutboxAsyncTest : RelationDatabaseOutboxAsyncTest
{
    protected override string DefaultConnectingString => Const.DefaultConnectingString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false; 
    
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new MsSqlOutbox(configuration);
    }

    protected override IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new MsSqlTransactionProvider(Configuration);
    }

    protected override async Task CreateOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await MsSqlTestHelper.EnsureDatabaseExistsAsync(configuration.ConnectionString);
        
        await using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SqlOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new SqlConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}
