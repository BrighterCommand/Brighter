using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.Outbox;

[Collection("Outbox")]
public class PostgresBinaryOutboxTest : PostgresTextOutboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
