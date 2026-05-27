using System.Linq;
using System.Text.Json;
using Newtonsoft.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Pins the wire-format parity decision for issue #4149: a Baggage value written by one
// JSON stack must read identically on the other. This is critical for persistence stacks
// that write outbox/inbox rows on one stack and consume them on the other.
public class BaggageStjNewtonsoftParityTests
{
    [Fact]
    public void When_Baggage_Is_Serialised_On_Stj_It_Deserialises_Identically_On_Newtonsoft()
    {
        var original = new Baggage();
        original.Add("user", "alice");
        original.Add("tenant", "acme");

        var stjJson = System.Text.Json.JsonSerializer.Serialize(original, JsonSerialisationOptions.Options);
        var roundTripped = JsonConvert.DeserializeObject<Baggage>(stjJson);

        Assert.NotNull(roundTripped);
        var entries = roundTripped!.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Equal("alice", entries["user"]);
        Assert.Equal("acme", entries["tenant"]);
    }

    [Fact]
    public void When_Baggage_Is_Serialised_On_Newtonsoft_It_Deserialises_Identically_On_Stj()
    {
        var original = new Baggage();
        original.Add("user", "alice");
        original.Add("tenant", "acme");

        var newtonsoftJson = JsonConvert.SerializeObject(original);
        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<Baggage>(newtonsoftJson, JsonSerialisationOptions.Options);

        Assert.NotNull(roundTripped);
        var entries = roundTripped!.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Equal("alice", entries["user"]);
        Assert.Equal("acme", entries["tenant"]);
    }
}
