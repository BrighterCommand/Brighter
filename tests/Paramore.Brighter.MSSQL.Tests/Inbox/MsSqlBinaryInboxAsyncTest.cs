namespace Paramore.Brighter.MSSQL.Tests.Inbox;

public class MsSqlBinaryInboxAsyncTest : MsSqlTextInboxAsyncTest
{
    protected override bool BinaryMessagePayload => false;
}
