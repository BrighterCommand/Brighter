using System.Linq;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Regression pin for issue #4173 — Sentry (and any W3C-conformant producer) emits the
// baggage header with optional whitespace (OWS) around the "," and "=" delimiters, e.g.
// "sentry-trace_id = abc, sentry-public_key = def". The W3C baggage spec
// (https://www.w3.org/TR/baggage/#definition) permits OWS around those delimiters, so the
// surrounding whitespace must be trimmed before the key/value are validated.
public class LoadBaggageWhitespaceTests
{
    [Fact]
    public void When_Loading_Baggage_With_Optional_Whitespace_Around_Delimiters_The_Entries_Are_Parsed()
    {
        // Arrange — a real Sentry baggage header with OWS around '=' and ','
        const string sentryBaggage =
            "sentry-trace_id = 5ad257b8d9100d1a5dd03b424db74400, " +
            "sentry-public_key = 1457b9474f0a1771a57da2a09fda6cdc, " +
            "sentry-sampled = true";

        // Act
        var baggage = Baggage.FromString(sentryBaggage);

        // Assert — keys and values are trimmed of the optional whitespace
        var entries = baggage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Equal("5ad257b8d9100d1a5dd03b424db74400", entries["sentry-trace_id"]);
        Assert.Equal("1457b9474f0a1771a57da2a09fda6cdc", entries["sentry-public_key"]);
        Assert.Equal("true", entries["sentry-sampled"]);
    }
}
