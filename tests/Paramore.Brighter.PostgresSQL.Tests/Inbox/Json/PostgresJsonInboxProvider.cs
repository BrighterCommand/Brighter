using Paramore.Brighter.PostgresSQL.Tests.Inbox.Text;
using Paramore.Brighter.PostgresSQL.Tests.Inbox.Json.Async;
using Paramore.Brighter.PostgresSQL.Tests.Inbox.Json.Sync;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox.Json;

public class PostgresJsonInboxProvider : PostgresInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public PostgresJsonInboxProvider() : base(false, true)
    {
    }
}
