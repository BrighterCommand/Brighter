namespace Paramore.Brighter.MySQL.Tests.Inbox;
[TUnit.Core.InheritsTests]

public class MySqlBinaryInboxAsyncTest : MySqlTextInboxAsyncTest
{
    protected override bool BinaryMessagePayload => false;
}
