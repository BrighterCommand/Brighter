using Oracle.ManagedDataAccess.Client;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Oracle;

namespace Paramore.Brighter.Oracle.Tests.Inbox;

public class OracleTextInboxTest : RelationalDatabaseInboxTests
{
    protected override string DefaultConnectingString => Const.DefaultConnectingString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    protected override bool JsonMessagePayload => false;

    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new OracleInbox(configuration);
    }

    protected override void CreateInboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new OracleConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = OracleInboxBuilder.GetDDL(
            configuration.InBoxTableName,
            BinaryMessagePayload,
            JsonMessagePayload);
        command.ExecuteNonQuery();
    }

    protected override void DeleteInboxTable(RelationalDatabaseConfiguration configuration)
    {
        using var connection = new OracleConnection(configuration.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        command.ExecuteNonQuery();
    }
}
