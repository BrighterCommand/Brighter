namespace Paramore.Brighter.Sqlite.Tests.Inbox;

public class SqliteBinaryInboxTest : SqliteTextInboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
