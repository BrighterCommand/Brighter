using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Proactor;

[Collection("MessagingGateway")]
public class PostgresLargePayloadProactorTests : PostgresProactorTests
{
    protected override string Prefix => "L";
    protected override bool LargePayload => true;
}
