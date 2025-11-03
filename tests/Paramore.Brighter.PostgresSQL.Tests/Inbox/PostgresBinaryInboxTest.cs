using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Inbox;

[Collection("Inbox")]
public class PostgresBinaryInboxTest : PostgresTextInboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
