namespace Paramore.Brighter.MSSQL.Tests.Outbox;

public class MsSqlBinaryOutboxTest : MsSqlTextOutboxTest
{
    protected override bool BinaryMessagePayload => true;
}
