using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

public class MySqlBinaryOutboxAsyncTest : MySqlTextOutboxAsyncTest
{
    protected override bool BinaryMessagePayload => true;
}
