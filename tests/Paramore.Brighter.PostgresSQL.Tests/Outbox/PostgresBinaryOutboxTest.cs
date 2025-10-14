namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

public class PostgresBinaryOutboxTest : PostgresTextOutboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
