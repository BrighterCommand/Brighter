using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox;

[Collection("Inbox")]
public class PostgresBinaryInboxAsyncTest : PostgresTextInboxAsyncTest 
{
    protected override bool BinaryMessagePayload => true;
}
