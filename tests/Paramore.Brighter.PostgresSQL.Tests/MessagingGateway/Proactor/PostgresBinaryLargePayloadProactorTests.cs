using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Proactor;

[Collection("MessagingGateway")]
public class PostgresBinaryLargePayloadProactorTests : PostgresProactorTests
{
    protected override string Prefix => "LB";
    protected override bool BinaryMessagePayload => true;
    protected override bool LargePayload => true;
}
