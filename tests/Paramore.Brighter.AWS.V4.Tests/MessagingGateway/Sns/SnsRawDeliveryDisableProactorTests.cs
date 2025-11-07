using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests.MessagingGateway.Sns;

public class SnsRawDeliveryDisableProactorTests : SnsProactorTests
{
    protected override bool RawMessageDelivery => false;
}
