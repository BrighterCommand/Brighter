using System.Data.Common;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.Spanner;
using Paramore.Brighter.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Spanner.Outbox;

[Trait("Category", "Spanner")]
public class SpannerTextOutboxTest : RelationDatabaseOutboxTest
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

    protected override void CreateOutboxTable(RelationalDatabaseConfiguration configuration)
    { 
        using var connection = new SpannerConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SpannerOutboxBuilder.GetDDL(configuration.OutBoxTableName, BinaryMessagePayload);
        command.ExecuteNonQuery();
    }

    protected override void DeleteOutboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new SpannerConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.OutBoxTableName}";
        command.ExecuteNonQuery();
    }
}
