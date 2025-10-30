namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Reactor;

public class PostgresBinaryLargePayloadReactorTests : PostgresReactorTests
{
    protected override string Prefix => "LB";
    protected override bool BinaryMessagePayload => true;
    protected override bool LargePayload => true;
}
