using System;
using System.Text.Json;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation
{
    public class MessageSerilisationTests
    {

        [Fact]
        public void When_I_Serialise_a_Message_I_Can_Deserialise_It()
        {
            var origionalMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "TestTopic", MessageType.MT_NONE, DateTime.Now, Guid.NewGuid(),
                    "ReplyTo", partitionKey: "PartitionKey"),
                new MessageBody("This is a Test Message"));

            var seralisedMessage = JsonSerializer.Serialize(origionalMessage, JsonSerialisationOptions.Options);

            var deserialisedMessage =
                JsonSerializer.Deserialize<Message>(seralisedMessage, JsonSerialisationOptions.Options);

            
            Assert.Equal(origionalMessage, deserialisedMessage);
        }
    }
}
