namespace Paramore.Brighter.MSSQL.Tests.Inbox;
[TUnit.Core.InheritsTests]

public class MsSqlBinaryInboxAsyncTest : MsSqlTextInboxAsyncTest
{
    protected override bool BinaryMessagePayload => false;
}
