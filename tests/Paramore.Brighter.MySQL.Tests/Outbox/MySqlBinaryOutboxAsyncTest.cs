using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

[Collection("Outbox")]
public class MySqlBinaryOutboxAsyncTest : MySqlTextOutboxAsyncTest
{
    protected override bool BinaryMessagePayload => true;
}
