using Paramore.Brighter.PostgresSQL.Tests.Inbox.Text;
using Paramore.Brighter.PostgresSQL.Tests.Inbox.Text.Async;
using Paramore.Brighter.PostgresSQL.Tests.Inbox.Text.Sync;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox.Text;

public class PostgresTextInboxProvider : PostgresInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public PostgresTextInboxProvider() : base(false, false)
    {
    }
}
