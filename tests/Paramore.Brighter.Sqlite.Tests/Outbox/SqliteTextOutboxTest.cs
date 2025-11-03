using System.Data.Common;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Collection("Outbox")]
public class SqliteTextOutboxTest : RelationDatabaseOutboxTest
{
    protected override string DefaultConnectingString => Tests.Configuration.ConnectionString;
    protected override string TableNamePrefix => Tests.Configuration.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new SqliteOutbox(configuration);
    }

    protected override void CreateOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        Tests.Configuration.CreateTable(configuration.ConnectionString, SqliteOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload));
    }

    protected override void DeleteOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        Tests.Configuration.DeleteTable(configuration.ConnectionString, configuration.OutBoxTableName);
    }

    protected override IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new SqliteTransactionProvider(Configuration);
    }
}
