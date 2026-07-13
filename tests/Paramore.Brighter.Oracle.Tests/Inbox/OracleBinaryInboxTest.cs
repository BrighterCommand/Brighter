namespace Paramore.Brighter.Oracle.Tests.Inbox;

public class OracleBinaryInboxTest : OracleTextInboxTest
{
    protected override bool BinaryMessagePayload => true;
}
