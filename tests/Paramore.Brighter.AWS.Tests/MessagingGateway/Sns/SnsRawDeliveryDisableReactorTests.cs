namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns;

public class SnsRawDeliveryDisableReactorTests : SnsReactorTests
{
    protected override bool RawMessageDelivery => false;
}
