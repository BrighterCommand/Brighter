namespace Paramore.Brighter.PostgresSQL.Tests.Inbox;

public class PostgresBinaryInboxTest : PostgresTextInboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
