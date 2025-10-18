namespace Paramore.Brighter.MySQL.Tests.Inbox;

public class MySqlBinaryInboxAsyncTest : MySqlTextInboxAsyncTest
{
    protected override bool BinaryMessagePayload => false;
}
