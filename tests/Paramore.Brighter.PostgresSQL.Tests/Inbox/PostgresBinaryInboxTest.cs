namespace Paramore.Brighter.PostgresSQL.Tests.Inbox;
[TUnit.Core.InheritsTests]

public class PostgresBinaryInboxTest : PostgresTextInboxTest
{
    protected override bool BinaryMessagePayload => true;
}
