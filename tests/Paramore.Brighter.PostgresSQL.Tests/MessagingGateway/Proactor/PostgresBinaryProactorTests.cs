using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Proactor;

[Collection("MessagingGateway")]
public class PostgresBinaryProactorTests  : PostgresProactorTests
{
    protected override string Prefix => "B";
    protected override bool BinaryMessagePayload => true;
}
