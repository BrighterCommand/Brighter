using Xunit;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

[Collection("Outbox")]
public class SqliteBinaryOutboxTest : SqliteTextOutboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
