using Xunit;

namespace Paramore.Brighter.MySQL.Tests.Inbox;

[Collection("Inbox")]
public class MySqlBinaryInboxTest : MySqlTextInboxTest 
{
    protected override bool BinaryMessagePayload  => true;
}
