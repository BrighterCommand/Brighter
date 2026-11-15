namespace Paramore.Brighter.MySQL.Tests.Inbox;
[TUnit.Core.InheritsTests]

public class MySqlBinaryInboxTest : MySqlTextInboxTest
{
    protected override bool BinaryMessagePayload  => true;
}
