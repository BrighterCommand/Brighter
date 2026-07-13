namespace Paramore.Brighter.Oracle.Tests.Inbox;

public class OracleJsonInboxTest : OracleTextInboxTest
{
    protected override bool JsonMessagePayload => true;
    protected override bool BinaryMessagePayload => true;
}
