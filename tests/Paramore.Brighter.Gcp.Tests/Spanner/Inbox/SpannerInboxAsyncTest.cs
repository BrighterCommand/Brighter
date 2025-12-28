using System.Threading.Tasks;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Spanner;

namespace Paramore.Brighter.Gcp.Tests.Spanner.Inbox;

[Trait("Category", "Spanner")]
public class SpannerInboxAsyncTest : RelationalDatabaseInboxAsyncTests
{
    protected override string DefaultConnectingString => Const.ConnectionString;
    protected override string TableNamePrefix => Const.TablePrefix; 
    protected override bool BinaryMessagePayload => false;
    protected override bool JsonMessagePayload => false;

    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new SpannerInboxAsync(configuration);
    }

    protected override async Task CreateInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new SpannerConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SpannerInboxBuilder.GetDDL(configuration.InBoxTableName);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteInboxTableAsync(RelationalDatabaseConfiguration configuration)
    { 
        await using var connection = new SpannerConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}
