using System;
using System.Collections.Generic;
using System.Text.Json;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

// These tests mutate the global JsonSerialisationOptions.Options, so they must not run in
// parallel with any other test that relies on the serialization policy. Grouping them in a
// single, parallelism-disabled collection isolates that shared-state change.
[CollectionDefinition("SerializationPolicy", DisableParallelization = true)]
public class SerializationPolicyCollection;

[Trait("Category", "ASB")]
[Collection("SerializationPolicy")]
public class AzureServiceBusMessagePublisherSessionIdTests
{
    public static IEnumerable<object?[]> NamingPolicies =>
    [
        // the default: a bag key written as "SessionId" round-trips as "sessionId"
        [JsonNamingPolicy.CamelCase],
        // a user-overridden policy: "SessionId" round-trips as "session_id" — the case a
        // case-insensitive-only fix cannot resolve
        [JsonNamingPolicy.SnakeCaseLower],
        // no policy: the key stays "SessionId"
        [null],
    ];

    [Theory]
    [MemberData(nameof(NamingPolicies))]
    public void When_the_session_id_bag_key_round_trips_under_the_configured_policy_should_set_the_session_id(
        JsonNamingPolicy? policy)
    {
        // Brighter's Outbox serializes the header Bag with JsonSerialisationOptions.Options, and its
        // PropertyNamingPolicy rewrites bag keys on the way out. A key written as "SessionId" therefore
        // comes back transformed by whatever policy is configured. The publisher must resolve the
        // SessionId regardless of the policy — and the reserved key must never leak onto the wire.
        var original = JsonSerialisationOptions.Options;
        try
        {
            JsonSerialisationOptions.Options = new JsonSerializerOptions(original) { PropertyNamingPolicy = policy };

            const string sessionIdKey = "SessionId"; // the reserved bag key as written by application code
            const string expectedSessionId = "order-42";
            var header = new MessageHeader(
                messageId: Guid.NewGuid().ToString(),
                topic: new RoutingKey("test.topic"),
                messageType: MessageType.MT_COMMAND);
            header.Bag[sessionIdKey] = expectedSessionId;

            // round-trip the Bag exactly as an Outbox does, so the key is mangled by the real policy
            var json = JsonSerializer.Serialize(header.Bag, JsonSerialisationOptions.Options);
            header.Bag = JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonSerialisationOptions.Options)!;

            var message = new Message(header, new MessageBody("body"));

            var asbMessage = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message);

            // the session id is set on the outgoing message...
            Assert.Equal(expectedSessionId, asbMessage.SessionId);
            // ...and the reserved header, whatever casing the policy gave it, does not leak into ApplicationProperties
            var roundTrippedKey = policy?.ConvertName(sessionIdKey) ?? sessionIdKey;
            Assert.False(asbMessage.ApplicationProperties.ContainsKey(roundTrippedKey));
        }
        finally
        {
            JsonSerialisationOptions.Options = original;
        }
    }
}
