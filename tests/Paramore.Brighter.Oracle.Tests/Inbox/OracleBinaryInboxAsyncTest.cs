namespace Paramore.Brighter.Oracle.Tests.Inbox;

public class OracleBinaryInboxAsyncTest : OracleTextInboxAsyncTest
{
    protected override bool BinaryMessagePayload => true;
}
