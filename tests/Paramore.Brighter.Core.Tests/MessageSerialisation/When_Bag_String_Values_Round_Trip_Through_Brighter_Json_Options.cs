using System.Collections.Generic;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

// Regression pin for the contract that OutboxProducerMediator.GetProducerLookupTopic
// relies on: when Header.Bag is round-tripped through Brighter's JsonSerialisationOptions
// the values come back as their runtime types (string here), NOT as JsonElement.
// If a future change drops DictionaryStringObjectJsonConverter or
// ObjectToInferredTypesConverter from the options, the `producerTopic is string` cast in
// GetProducerLookupTopic would silently fail for persistent outboxes (SQL, Mongo,
// DynamoDB) and reply-message dispatch would regress to the bug this PR fixes.
public class BagStringValueRoundTripTests
{
    [Test]
    public async Task When_Round_Tripping_A_Bag_String_Value_It_Survives_As_String_Not_JsonElement()
    {
        var bag = new Dictionary<string, object>
        {
            [Message.ProducerTopicHeaderName] = "the.publication.topic",
            ["customer.header"] = "customer.value"
        };

        var json = JsonSerializer.Serialize(bag, JsonSerialisationOptions.Options);
        var roundTripped = JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonSerialisationOptions.Options);

        await Assert.That(roundTripped).IsNotNull();
        await Assert.That(roundTripped![Message.ProducerTopicHeaderName] is string).IsTrue();
        await Assert.That(roundTripped[Message.ProducerTopicHeaderName]).IsEqualTo("the.publication.topic");
        await Assert.That(roundTripped["customer.header"] is string).IsTrue();
    }
}
