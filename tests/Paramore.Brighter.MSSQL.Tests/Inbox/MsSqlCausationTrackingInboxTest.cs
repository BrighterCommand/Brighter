using Paramore.Brighter.Base.Test.Inbox;
using Paramore.Brighter.Inbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Inbox;

[Trait("Category", "MSSQL")]
public class MsSqlCausationTrackingInboxTest : CausationTrackingInboxBaseTests
{
    private RelationalDatabaseConfiguration _configuration = null!;
    private MsSqlInbox _inbox = null!;

    protected override IAmAnInboxSync Inbox => _inbox;

    protected override void BeforeEachTest()
    {
        _configuration = new RelationalDatabaseConfiguration(
            Tests.Configuration.DefaultConnectingString,
            inboxTableName: $"{Tests.Configuration.TablePrefix}{Uuid.New():N}");
        _inbox = new MsSqlInbox(_configuration);
        base.BeforeEachTest();
    }

    protected override void CreateStore()
    {
        Tests.Configuration.EnsureDatabaseExists(_configuration.ConnectionString);
        Tests.Configuration.CreateTable(_configuration.ConnectionString,
            SqlInboxBuilder.GetDDL(_configuration.InBoxTableName));
    }

    protected override void DeleteStore()
    {
        Tests.Configuration.DeleteTable(_configuration.ConnectionString, _configuration.InBoxTableName);
    }
}
