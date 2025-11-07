namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Reactor;

public class PostgresLargePayloadReactorTests : PostgresReactorTests
{
    protected override string Prefix => "L";
    protected override bool LargePayload => true;
}
