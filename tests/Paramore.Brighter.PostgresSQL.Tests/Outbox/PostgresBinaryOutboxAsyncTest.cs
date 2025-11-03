using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

[Collection("Outbox")]
public class PostgresBinaryOutboxAsyncTest : PostgresTextOutboxAsyncTest 
{
    protected override bool BinaryMessagePayload => true;
}
