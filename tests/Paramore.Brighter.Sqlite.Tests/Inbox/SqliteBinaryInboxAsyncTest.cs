namespace Paramore.Brighter.Sqlite.Tests.Inbox;
[TUnit.Core.InheritsTests]

public class SqliteBinaryInboxAsyncTest : SqliteTextInboxAsyncTest
{
    protected override bool BinaryMessagePayload => true;
}
