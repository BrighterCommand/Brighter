using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Inbox;

[Collection("Inbox")]
public class MySqlBinaryInboxAsyncTest : MySqlTextInboxAsyncTest
{
    protected override bool BinaryMessagePayload => false;
}
