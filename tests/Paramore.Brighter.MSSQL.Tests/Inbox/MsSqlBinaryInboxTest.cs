using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Inbox;

[Collection("Inbox")]
public class MsSqlBinaryInboxTest : MsSqlTextInboxTest 
{
    protected override bool BinaryMessagePayload  => true;
}
