namespace Paramore.Brighter.Gcp.Tests.Spanner.Outbox;

public class SpannerBinaryOutboxAsyncTest : SpannerTextOutboxAsyncTest
{
    protected override bool BinaryMessagePayload => true;
}
