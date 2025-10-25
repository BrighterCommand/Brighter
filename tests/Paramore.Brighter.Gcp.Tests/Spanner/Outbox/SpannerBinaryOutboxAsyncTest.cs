namespace Paramore.Brighter.Gcp.Tests.Spanner.Outbox;

[Trait("Category", "Spanner")]
public class SpannerBinaryOutboxAsyncTest : SpannerTextOutboxAsyncTest
{
    protected override bool BinaryMessagePayload => true;
}
