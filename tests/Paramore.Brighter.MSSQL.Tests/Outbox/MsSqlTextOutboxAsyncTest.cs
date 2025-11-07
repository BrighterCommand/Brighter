using System.Data.Common;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

[Collection("Outbox")]
public class MsSqlTextOutboxAsyncTest : RelationDatabaseOutboxAsyncTest
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

    protected override async Task CreateOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await Tests.Configuration.EnsureDatabaseExistsAsync(configuration.ConnectionString);
        await Tests.Configuration.CreateTableAsync(configuration.ConnectionString, SqlOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload));

    }

    protected override async Task DeleteOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await Tests.Configuration.DeleteTableAsync(configuration.ConnectionString, configuration.OutBoxTableName);
    }
}
