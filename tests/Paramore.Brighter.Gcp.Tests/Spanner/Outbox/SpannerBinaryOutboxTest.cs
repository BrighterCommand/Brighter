namespace Paramore.Brighter.Gcp.Tests.Spanner.Outbox;

[Trait("Category", "Spanner")]
public class SpannerBinaryOutboxTest : SpannerTextOutboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
