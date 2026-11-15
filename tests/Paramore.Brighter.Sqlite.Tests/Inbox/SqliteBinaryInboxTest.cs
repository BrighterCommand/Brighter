namespace Paramore.Brighter.Sqlite.Tests.Inbox;
[TUnit.Core.InheritsTests]

public class SqliteBinaryInboxTest : SqliteTextInboxTest
{
    protected override bool BinaryMessagePayload => true;
}
