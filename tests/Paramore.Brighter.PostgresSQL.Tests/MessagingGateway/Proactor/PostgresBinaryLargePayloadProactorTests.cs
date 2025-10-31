namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Proactor;

public class PostgresBinaryLargePayloadProactorTests : PostgresProactorTests
{
    protected override string Prefix => "LB";
    protected override bool BinaryMessagePayload => true;
    protected override bool LargePayload => true;
}
