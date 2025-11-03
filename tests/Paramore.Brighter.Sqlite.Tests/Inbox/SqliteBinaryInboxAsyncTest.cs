using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox;

[Collection("Inbox")]
public class SqliteBinaryInboxAsyncTest : SqliteTextInboxAsyncTest 
{
    protected override bool BinaryMessagePayload => true;
}
