using System.Data.Common;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

public class MsSqlTextOutboxTest : RelationDatabaseOutboxTest
{
    protected override string DefaultConnectingString => Tests.Configuration.DefaultConnectingString;
    protected override string TableNamePrefix => Tests.Configuration.TablePrefix;
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
        Tests.Configuration.EnsureDatabaseExists(configuration.ConnectionString);
        Tests.Configuration.CreateTable(configuration.ConnectionString, SqlOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload));
    }

    protected override void DeleteOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        Tests.Configuration.DeleteTable(configuration.ConnectionString, configuration.OutBoxTableName);
    }
}
