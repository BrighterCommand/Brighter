namespace Paramore.Brighter.Gcp.Tests.Spanner.Outbox;

public class SpannerBinaryOutboxTest : SpannerTextOutboxTest 
{
    protected override bool BinaryMessagePayload => true;
}
