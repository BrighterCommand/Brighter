using Paramore.Brighter.PostgresSQL.Tests.Inbox.Text;
using Paramore.Brighter.PostgresSQL.Tests.Inbox.Jsonb.Async;
using Paramore.Brighter.PostgresSQL.Tests.Inbox.Jsonb.Sync;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox.Jsonb;

public class PostgresJsonbInboxProvider : PostgresInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public PostgresJsonbInboxProvider() : base(true, true)
    {
    }
}
