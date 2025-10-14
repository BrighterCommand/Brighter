using System.Data.Common;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

public class MsSqlTextOutboxTest : RelationDatabaseOutboxTest
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

    protected override void CreateOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        MsSqlTestHelper.EnsureDatabaseExists(configuration.ConnectionString);
        
        using var connection = new SqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SqlOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    protected override void DeleteOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new SqlConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }
}
