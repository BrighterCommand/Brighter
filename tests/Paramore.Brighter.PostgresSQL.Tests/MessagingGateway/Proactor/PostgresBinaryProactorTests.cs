namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Proactor;

public class PostgresBinaryProactorTests  : PostgresProactorTests
{
    protected override string Prefix => "B";
    protected override bool BinaryMessagePayload => true;
}
