using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Reactor;

[Collection("MessagingGateway")]
public class PostgresLargePayloadReactorTests : PostgresReactorTests
{
    protected override string Prefix => "L";
    protected override bool LargePayload => true;
}
