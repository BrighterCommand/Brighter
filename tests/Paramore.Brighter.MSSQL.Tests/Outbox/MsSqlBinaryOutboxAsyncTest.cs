using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Outbox;

[Collection("Outbox")]
public class MsSqlBinaryOutboxAsyncTest : MsSqlTextOutboxAsyncTest
{
    protected override bool BinaryMessagePayload => true;
}
