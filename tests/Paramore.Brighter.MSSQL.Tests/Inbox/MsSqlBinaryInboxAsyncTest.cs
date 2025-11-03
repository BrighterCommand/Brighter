using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.Inbox;

[Collection("Inbox")]
public class MsSqlBinaryInboxAsyncTest : MsSqlTextInboxAsyncTest
{
    protected override bool BinaryMessagePayload => false;
}
