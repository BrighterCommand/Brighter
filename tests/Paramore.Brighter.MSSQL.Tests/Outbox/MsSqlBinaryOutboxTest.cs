using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

[Collection("Outbox")]
public class MsSqlBinaryOutboxTest : MsSqlTextOutboxTest
{
    protected override bool BinaryMessagePayload => true;
}
