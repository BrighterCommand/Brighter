using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Spanner.Inbox;

public class SpannerInboxTest : RelationalDatabaseInboxTests
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix; 
    protected override bool BinaryMessagePayload => false;
    
    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new SpannerInboxAsync(configuration);
    }

    protected override void CreateInboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new SpannerConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SpannerInboxBuilder.GetDDL(configuration.InBoxTableName);
        command.ExecuteNonQuery();
    }

    protected override void DeleteInboxTable(RelationalDatabaseConfiguration configuration)
    { 
        using var connection = new SpannerConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }
}
