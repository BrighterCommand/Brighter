namespace Paramore.Brighter.MySQL.Tests.Inbox;

public class MySqlBinaryInboxTest : MySqlTextInboxTest 
{
    protected override bool BinaryMessagePayload  => true;
}
