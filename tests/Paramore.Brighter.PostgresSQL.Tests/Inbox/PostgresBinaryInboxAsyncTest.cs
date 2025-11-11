namespace Paramore.Brighter.PostgresSQL.Tests.Inbox;

public class PostgresBinaryInboxAsyncTest : PostgresTextInboxAsyncTest 
{
    protected override bool BinaryMessagePayload => true;
}
