namespace Paramore.Brighter.PostgresSQL.Tests.Inbox;
[TUnit.Core.InheritsTests]

public class PostgresBinaryInboxAsyncTest : PostgresTextInboxAsyncTest
{
    protected override bool BinaryMessagePayload => true;
}
