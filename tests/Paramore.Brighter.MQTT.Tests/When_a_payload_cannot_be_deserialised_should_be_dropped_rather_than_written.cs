using System.Text;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessagingGateway.MQTT;
using Xunit;

namespace Paramore.Brighter.MQTT.Tests;

/// <summary>
/// The arrival handler's contract for a payload it cannot turn into a <see cref="Message"/>.
/// </summary>
/// <remarks>
/// <para>
/// MQTTnet hands every arriving payload to a callback registered on its own dispatch loop. The
/// handler deserialises that payload and writes the result into the channel the consumer's
/// <c>Receive</c> reads from. Neither step is safe on a payload someone else published: a literal
/// <c>null</c> document deserialises to <c>null</c>, and a malformed one throws.
/// </para>
/// <para>
/// A <c>null</c> written into the channel reaches the pump, which dereferences
/// <c>Message.Header</c> and fails on an unrelated thread. A throw escapes into MQTTnet's dispatch
/// loop rather than into any caller. Either way one poison message published by anybody on the
/// topic takes down a consumer, so the handler must drop what it cannot read.
/// </para>
/// <para>
/// Exercised without a broker, which is why the deserialisation step is a separate internal method
/// — the same shape, and for the same reason, as
/// <c>RocketMqMessageProducer.AddHeaderProperties</c>.
/// </para>
/// </remarks>
public class MqttPayloadDeserialisationTests
{
    [Fact]
    public void When_a_payload_cannot_be_deserialised_should_be_dropped_rather_than_written()
    {
        // Arrange — the two payloads a publisher can put on the topic that the handler cannot
        // turn into a message: one that deserialises to null, one that does not parse at all
        var deserialisesToNull = Encoding.UTF8.GetBytes("null");
        var malformed = Encoding.UTF8.GetBytes("{ this is not json");

        // Act
        var fromNull = MqttMessageConsumer.TryDeserialiseMessage(deserialisesToNull, "test/topic");
        var fromMalformed = MqttMessageConsumer.TryDeserialiseMessage(malformed, "test/topic");

        // Assert — both are dropped, and neither throws out of the handler
        Assert.Null(fromNull);
        Assert.Null(fromMalformed);
    }

    [Fact]
    public void When_a_payload_is_a_valid_message_should_be_returned_for_writing()
    {
        // Arrange — a well-formed message, so the test above cannot pass by dropping everything
        var message = new Message(
            new MessageHeader(Id.Random(), new RoutingKey("test/topic"), MessageType.MT_EVENT),
            new MessageBody("{ \"value\": 42 }"));
        var payload = Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(message, JsonSerialisationOptions.Options));

        // Act
        var deserialised = MqttMessageConsumer.TryDeserialiseMessage(payload, "test/topic");

        // Assert
        Assert.NotNull(deserialised);
        Assert.Equal(message.Body.Value, deserialised!.Body.Value);
    }
}
