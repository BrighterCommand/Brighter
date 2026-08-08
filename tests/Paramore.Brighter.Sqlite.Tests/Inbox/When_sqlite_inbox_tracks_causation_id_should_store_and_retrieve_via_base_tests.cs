using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.Sqlite;
using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox;

[Trait("Category", "Sqlite")]
public class SqliteCausationTrackingInboxTest : CausationTrackingInboxBaseTests
{
    private RelationalDatabaseConfiguration _configuration = null!;
    private SqliteInbox _inbox = null!;

    protected override IAmAnInboxSync Inbox => _inbox;

    protected override void BeforeEachTest()
    {
        _configuration = new RelationalDatabaseConfiguration(
            Tests.Configuration.ConnectionString,
            inboxTableName: $"{Tests.Configuration.TablePrefix}{Uuid.New():N}");
        _inbox = new SqliteInbox(_configuration);
        base.BeforeEachTest();
    }

    protected override void CreateStore()
    {
        Tests.Configuration.CreateTable(_configuration.ConnectionString,
            SqliteInboxBuilder.GetDDL(_configuration.InBoxTableName, false));
    }

    protected override void DeleteStore()
    {
        Tests.Configuration.DeleteTable(_configuration.ConnectionString, _configuration.InBoxTableName);
    }
}
