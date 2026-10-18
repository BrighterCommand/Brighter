using Paramore.Brighter.Sqlite.Tests.Inbox.Text.Async;
using Paramore.Brighter.Sqlite.Tests.Inbox.Text.Sync;

namespace Paramore.Brighter.Sqlite.Tests.Inbox.Text;

public class SqliteTextInboxProvider : SqliteInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public SqliteTextInboxProvider() : base(false)
    {
    }
}
