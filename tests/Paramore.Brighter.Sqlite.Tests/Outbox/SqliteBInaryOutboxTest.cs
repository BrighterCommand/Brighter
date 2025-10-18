namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class SqliteBinaryOutboxTest : SqliteTextOutboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
