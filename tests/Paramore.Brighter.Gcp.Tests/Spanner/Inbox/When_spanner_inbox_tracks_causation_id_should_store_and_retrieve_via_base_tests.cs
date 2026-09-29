using Google.Api.Gax;
using Google.Cloud.Spanner.Data;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Spanner;


namespace Paramore.Brighter.Gcp.Tests.Spanner.Inbox;

[Property("Category", "Spanner")]
[TUnit.Core.InheritsTests]
public class SpannerCausationTrackingInboxTest : CausationTrackingInboxBaseTests
{
    private RelationalDatabaseConfiguration _configuration = null!;
    private SpannerInboxAsync _inbox = null!;

    protected override IAmAnInboxSync Inbox => _inbox;

    protected override void BeforeEachTest()
    {
        var connectionString = new SpannerConnectionStringBuilder(Const.ConnectionString)
        {
            EmulatorDetection = EmulatorDetection.EmulatorOrProduction
        }.ConnectionString;

        _configuration = new RelationalDatabaseConfiguration(
            connectionString,
            inboxTableName: $"{Const.TablePrefix}{Uuid.New():N}");
        _inbox = new SpannerInboxAsync(_configuration);
        base.BeforeEachTest();
    }

    protected override void CreateStore()
    {
        using var connection = new SpannerConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SpannerInboxBuilder.GetDDL(_configuration.InBoxTableName);
        command.ExecuteNonQuery();
    }

    protected override void DeleteStore()
    {
        using var connection = new SpannerConnection(_configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {_configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }
}
