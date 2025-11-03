using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Collection("Outbox")]
public class SqliteBinaryOutboxAsyncTest : SqliteTextOutboxAsyncTest 
{
    protected override bool BinaryMessagePayload => true;
}
