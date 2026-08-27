using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Oracle;

namespace Paramore.Brighter.Oracle.Tests.Inbox;

public class OracleTextInboxAsyncTest : RelationalDatabaseInboxAsyncTests
{
    protected override string DefaultConnectingString => Const.DefaultConnectingString;
    protected override string TableNamePrefix => Const.TablePrefix;
    protected override bool BinaryMessagePayload => false;
    protected override bool JsonMessagePayload => false;

    protected override RelationalDatabaseInbox CreateInbox(RelationalDatabaseConfiguration configuration)
    {
        return new OracleInbox(configuration);
    }

    protected override async Task CreateInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new OracleConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = OracleInboxBuilder.GetDDL(
            configuration.InBoxTableName,
            BinaryMessagePayload,
            JsonMessagePayload);
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task DeleteInboxTableAsync(RelationalDatabaseConfiguration configuration)
    {
        await using var connection = new OracleConnection(configuration.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE {configuration.InBoxTableName}";
        await command.ExecuteNonQueryAsync();
    }
}
