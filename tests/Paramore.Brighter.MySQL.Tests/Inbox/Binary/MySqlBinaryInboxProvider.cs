using Paramore.Brighter.MySQL.Tests.Inbox.Text;
using Paramore.Brighter.MySQL.Tests.Inbox.Binary.Async;
using Paramore.Brighter.MySQL.Tests.Inbox.Binary.Sync;

namespace Paramore.Brighter.MySQL.Tests.Inbox.Binary;

public class MySqlBinaryInboxProvider : MySqlInboxProviderBase, IAmAnInboxProviderSync, IAmAnInboxProviderAsync
{
    public MySqlBinaryInboxProvider() : base(true, false)
    {
    }
}
