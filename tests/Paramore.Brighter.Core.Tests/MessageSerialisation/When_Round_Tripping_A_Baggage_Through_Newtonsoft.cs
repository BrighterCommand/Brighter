using System.Linq;
using Newtonsoft.Json;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Regression pin for issue #4149 (symptom 2) — Baggage implements
// IEnumerable<KeyValuePair<string, string?>> but exposes Add(string, string) only.
// Without a Newtonsoft converter, Newtonsoft infers a list-shaped target, finds no
// matching Add(KeyValuePair<...>) overload, and throws JsonSerializationException on
// deserialization. The fix ships NBaggageConverter and attributes it on the class so
// default JsonConvert calls pick it up.
public class BaggageNewtonsoftRoundTripTests
{
    [Fact]
    public void When_Round_Tripping_A_Baggage_Through_Newtonsoft_The_Entries_Survive()
    {
        var baggage = new Baggage();
        baggage.Add("user", "alice");
        baggage.Add("tenant", "acme");

        var json = JsonConvert.SerializeObject(baggage);
        var roundTripped = JsonConvert.DeserializeObject<Baggage>(json);

        Assert.NotNull(roundTripped);
        var entries = roundTripped!.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Equal("alice", entries["user"]);
        Assert.Equal("acme", entries["tenant"]);
    }
}
