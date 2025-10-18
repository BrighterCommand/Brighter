namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

public class PostgresBinaryOutboxAsyncTest : PostgresTextOutboxAsyncTest 
{
    protected override bool BinaryMessagePayload => true;
}
