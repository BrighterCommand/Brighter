using System.Text.Json;

namespace Paramore.Brighter.AWS.Tests.TestDoubles
{
    internal class MyDeferredCommandMessageMapper : IAmAMessageMapper<MyDeferredCommand>
    {
        private readonly string _topicName;

        public MyDeferredCommandMessageMapper(string topicName)
        {
            _topicName = topicName;
        }

        public Message MapToMessage(MyDeferredCommand request)
        {
            var header = new MessageHeader(messageId: request.Id, topic: _topicName, messageType: MessageType.MT_COMMAND);
            var body = new MessageBody(System.Text.Json.JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)));
            var message = new Message(header, body);
            return message;
        }

        public MyDeferredCommand MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<MyDeferredCommand>(message.Body.Value, JsonSerialisationOptions.Options);
        }
    }
}
