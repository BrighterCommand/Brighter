using Paramore.Brighter.Sqlite.Tests.Inbox.Text;
using Paramore.Brighter.Sqlite.Tests.Inbox.Binary.Async;
using Paramore.Brighter.Sqlite.Tests.Inbox.Binary.Sync;

namespace Paramore.Brighter.Sqlite.Tests.Inbox.Binary;

public class SqliteBinaryInboxProvider : SqliteInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public SqliteBinaryInboxProvider() : base(true)
    {
    }
}
