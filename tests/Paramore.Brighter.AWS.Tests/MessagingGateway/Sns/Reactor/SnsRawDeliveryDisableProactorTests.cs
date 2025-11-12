using Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Proactor;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Reactor;

public class SnsRawDeliveryDisableProactorTests : SnsProactorTests
{
    protected override bool RawMessageDelivery => false;
}
