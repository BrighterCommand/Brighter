using Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Reactor;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Proactor;

public class SnsRawDeliveryDisableReactorTests : SnsReactorTests
{
    protected override bool RawMessageDelivery => false;
}
