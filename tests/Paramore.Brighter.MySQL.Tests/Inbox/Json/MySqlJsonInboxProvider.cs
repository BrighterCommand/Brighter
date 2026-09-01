using Paramore.Brighter.MySQL.Tests.Inbox.Text;
using Paramore.Brighter.MySQL.Tests.Inbox.Json.Async;
using Paramore.Brighter.MySQL.Tests.Inbox.Json.Sync;

namespace Paramore.Brighter.MySQL.Tests.Inbox.Json;

public class MySqlJsonInboxProvider : MySqlInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public MySqlJsonInboxProvider() : base(false, true)
    {
    }
}
