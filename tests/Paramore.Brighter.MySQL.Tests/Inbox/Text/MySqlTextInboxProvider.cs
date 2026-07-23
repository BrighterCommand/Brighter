using Paramore.Brighter.MySQL.Tests.Inbox.Text.Async;
using Paramore.Brighter.MySQL.Tests.Inbox.Text.Sync;

namespace Paramore.Brighter.MySQL.Tests.Inbox.Text;

public class MySqlTextInboxProvider : MySqlInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public MySqlTextInboxProvider() : base(false, false)
    {
    }
}
