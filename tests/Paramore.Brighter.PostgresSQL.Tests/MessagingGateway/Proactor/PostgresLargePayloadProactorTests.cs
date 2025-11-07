namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Proactor;

public class PostgresLargePayloadProactorTests : PostgresProactorTests
{
    protected override string Prefix => "L";
    protected override bool LargePayload => true;
}
