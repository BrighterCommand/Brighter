using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Reactor;

[Collection("MessagingGateway")]
public class PostgresBinaryReactorTests  : PostgresReactorTests
{
    protected override string Prefix => "B";
    protected override bool BinaryMessagePayload => true;
}
