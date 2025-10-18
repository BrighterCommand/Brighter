using System.Data.Common;
using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.Spanner;
using Paramore.Brighter.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Spanner.Outbox;

[Trait("Category", "Spanner")]
public class SpannerTextOutboxAsyncTest : RelationDatabaseOutboxAsyncTest
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix; 
    protected override bool BinaryMessagePayload => false;
    
    protected override IAmABoxTransactionProvider<DbTransaction> CreateTransactionProvider()
    {
        return new SpannerUnitOfWork(Configuration);
    }
    
    protected override RelationDatabaseOutbox CreateOutbox(RelationalDatabaseConfiguration configuration)
    {
        return new SpannerOutbox(configuration);
    }

    protected override async Task CreateOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    { 
        await using var connection = new SpannerConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SpannerOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteOutboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new SpannerConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.OutBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}
