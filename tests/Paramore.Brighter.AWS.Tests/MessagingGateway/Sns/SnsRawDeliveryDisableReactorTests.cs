using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns;

[Collection("MessagingGateway")]
public class SnsRawDeliveryDisableReactorTests : SnsReactorTests
{
    protected override bool RawMessageDelivery => false;
}
