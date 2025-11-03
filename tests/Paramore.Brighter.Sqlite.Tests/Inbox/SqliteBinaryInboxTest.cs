using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Inbox;

[Collection("Inbox")]
public class SqliteBinaryInboxTest : SqliteTextInboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
