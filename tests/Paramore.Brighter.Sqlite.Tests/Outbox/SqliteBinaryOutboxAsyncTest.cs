namespace Paramore.Brighter.Sqlite.Tests.Outbox;

public class SqliteBinaryOutboxAsyncTest : SqliteTextOutboxAsyncTest 
{
    protected override bool BinaryMessagePayload => true;
}
