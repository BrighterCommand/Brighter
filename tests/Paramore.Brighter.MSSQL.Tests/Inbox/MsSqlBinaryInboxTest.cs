namespace Paramore.Brighter.MSSQL.Tests.Inbox;

public class MsSqlBinaryInboxTest : MsSqlTextInboxTest 
{
    protected override bool BinaryMessagePayload  => true;
}
