namespace Paramore.Brighter.MSSQL.Tests.Outbox;

public class MsSqlBinaryOutboxAsyncTest : MsSqlTextOutboxAsyncTest
{
    protected override bool BinaryMessagePayload => true;
}
