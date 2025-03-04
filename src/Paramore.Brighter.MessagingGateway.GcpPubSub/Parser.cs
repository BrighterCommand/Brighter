using Google.Cloud.PubSub.V1;
using Google.Protobuf;

namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

internal static class Parser
{
    public static Message ToBrighterMessage(ReceivedMessage receivedMessage)
    {
        var header = new 
    }


    public static PubsubMessage ToPubsubMessage(Message message)
    {
        var pubSubMessage = new PubsubMessage
        {
            Data = ByteString.CopyFrom(message.Body.Bytes), OrderingKey = message.Header.PartitionKey
        };
    }
}
