using Paramore.Brighter.MSSQL.Tests.Inbox.MSSQLText.Async;
using Paramore.Brighter.MSSQL.Tests.Inbox.MSSQLText.Sync;

namespace Paramore.Brighter.MSSQL.Tests.Inbox.MSSQLText;

public class MSSQLTextInboxProvider : MSSQLInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public MSSQLTextInboxProvider() : base(false)
    {
    }
}
