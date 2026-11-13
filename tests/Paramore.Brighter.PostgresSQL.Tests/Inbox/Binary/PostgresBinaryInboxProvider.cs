using Paramore.Brighter.PostgresSQL.Tests.Inbox.Text;
using Paramore.Brighter.PostgresSQL.Tests.Inbox.Binary.Async;
using Paramore.Brighter.PostgresSQL.Tests.Inbox.Binary.Sync;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox.Binary;

public class PostgresBinaryInboxProvider : PostgresInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public PostgresBinaryInboxProvider() : base(true, false)
    {
    }
}
