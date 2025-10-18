namespace Paramore.Brighter.Sqlite.Tests.Inbox;

public class SqliteBinaryInboxAsyncTest : SqliteTextInboxAsyncTest 
{
    protected override bool BinaryMessagePayload => true;
}
