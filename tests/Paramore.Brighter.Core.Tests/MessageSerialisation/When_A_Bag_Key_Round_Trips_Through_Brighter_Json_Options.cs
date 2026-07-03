using System.Collections.Generic;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Root-cause pin for #4151 / #4054: Header.Bag keys are arbitrary user identifiers, not C#
// property names, so they must round-trip verbatim through Brighter's serialization. Applying
// PropertyNamingPolicy (camelCase by default) to dictionary keys rewrites "SessionId" to
// "sessionId", which silently breaks consumers that look the key up by its original name.
public class BagKeyRoundTripTests
{
    [Theory]
    [InlineData("SessionId")]   // PascalCase — the key that camelCase mangles to "sessionId"
    [InlineData("PartitionKey")]
    [InlineData("customer.header")] // already lowercase — must also be untouched
    public void When_Round_Tripping_A_Bag_Key_It_Survives_Verbatim(string key)
    {
        var bag = new Dictionary<string, object> { [key] = "a-value" };

        var json = JsonSerializer.Serialize(bag, JsonSerialisationOptions.Options);
        var roundTripped = JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonSerialisationOptions.Options);

        Assert.NotNull(roundTripped);
        Assert.True(roundTripped!.ContainsKey(key), $"expected the bag key '{key}' to survive verbatim");
        Assert.Equal("a-value", roundTripped[key]);
    }

    [Fact]
    public void When_Round_Tripping_A_Message_Header_A_Pascal_Case_Bag_Key_Is_Readable()
    {
        //guards the real user-facing path: the Bag as wired into MessageHeader serialization,
        //not just a bare dictionary through the converter
        var header = new MessageHeader(
            messageId: Id.Random(),
            topic: new RoutingKey("Test.Topic"),
            messageType: MessageType.MT_EVENT);
        header.Bag["SessionId"] = "order-42";

        var json = JsonSerializer.Serialize(header, JsonSerialisationOptions.Options);
        var roundTripped = JsonSerializer.Deserialize<MessageHeader>(json, JsonSerialisationOptions.Options);

        Assert.NotNull(roundTripped);
        Assert.True(roundTripped!.Bag.TryGetValue("SessionId", out var sessionId),
            "expected the PascalCase bag key 'SessionId' to be readable after a MessageHeader round-trip");
        Assert.Equal("order-42", sessionId);
    }
}
