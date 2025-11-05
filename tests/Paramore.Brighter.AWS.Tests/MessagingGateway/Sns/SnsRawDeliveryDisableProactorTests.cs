using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns;

[Collection("MessagingGateway")]
public class SnsRawDeliveryDisableProactorTests : SnsProactorTests
{
    protected override bool RawMessageDelivery => false;
}
