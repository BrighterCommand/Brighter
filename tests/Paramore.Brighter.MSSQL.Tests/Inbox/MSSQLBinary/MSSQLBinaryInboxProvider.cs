using Paramore.Brighter.MSSQL.Tests.Inbox.MSSQLText;
using Paramore.Brighter.MSSQL.Tests.Inbox.MSSQLBinary.Async;
using Paramore.Brighter.MSSQL.Tests.Inbox.MSSQLBinary.Sync;

namespace Paramore.Brighter.MSSQL.Tests.Inbox.MSSQLBinary;

public class MSSQLBinaryInboxProvider : MSSQLInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public MSSQLBinaryInboxProvider() : base(true)
    {
    }
}
