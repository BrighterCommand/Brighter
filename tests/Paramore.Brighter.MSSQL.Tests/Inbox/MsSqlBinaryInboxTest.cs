namespace Paramore.Brighter.MSSQL.Tests.Inbox;
[TUnit.Core.InheritsTests]

public class MsSqlBinaryInboxTest : MsSqlTextInboxTest
{
    protected override bool BinaryMessagePayload  => true;
}
