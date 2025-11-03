using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Outbox;

[Collection("Outbox")]
public class MySqlBinaryOutboxTest : MySqlTextOutboxTest
{
    protected override bool BinaryMessagePayload => true;
}
