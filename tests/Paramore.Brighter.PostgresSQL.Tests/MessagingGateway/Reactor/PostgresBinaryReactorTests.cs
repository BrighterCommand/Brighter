namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Reactor;

public class PostgresBinaryReactorTests  : PostgresReactorTests
{
    protected override string Prefix => "B";
    protected override bool BinaryMessagePayload => true;
}
