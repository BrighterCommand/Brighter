namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns;

public class SnsRawDeliveryDisableProactorTests : SnsProactorTests
{
    protected override bool RawMessageDelivery => false;
}
