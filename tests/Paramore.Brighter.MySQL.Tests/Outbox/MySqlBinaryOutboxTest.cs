using System.Collections.Generic;
using MySqlConnector;
using Paramore.Brighter.Base.Test.Outbox;
using Paramore.Brighter.Outbox.MySql;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

public class MySqlBinaryOutboxTest : MySqlTextOutboxTest
{
    protected override bool BinaryMessagePayload => true;
}
