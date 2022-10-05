using System.Text.Json;
using OpenTelemetry.Shared.Commands;
using OpenTelemetry.Shared.Events;
using Paramore.Brighter;

namespace OpenTelemetry.Shared.Mappers;

public class MessageMapper<T> : IAmAMessageMapper<T> where T : class, IRequest
{
    private readonly TopicDictionary _topics;

    public MessageMapper(TopicDictionary topics)
    {
        _topics = topics;
    }
    public Message MapToMessage(T request)
    {
        MessageType messageType;
        if (request is ICommand)
            messageType = MessageType.MT_COMMAND;
        else if (request is IEvent)
            messageType = MessageType.MT_EVENT;
        else
        {
            throw new ArgumentException("This message mapper can only map Commands and Events", nameof(request));
        }

        var topicName = _topics.GetTopic(typeof(T));

        var header = new MessageHeader(messageId: request.Id, topic: topicName, messageType: messageType);
        var body = new MessageBody(JsonSerializer.Serialize(request));
        var message = new Message(header, body);
        return message;
    }

    public T MapToRequest(Message message)
    {
        return JsonSerializer.Deserialize<T>(message.Body.Value, JsonSerialisationOptions.Options);
    }
}

public class TopicDictionary
{
    private readonly Dictionary<Type, string> _topics = new Dictionary<Type, string>();

    public TopicDictionary()
    {
        _topics.Add(typeof(MyDistributedEvent), "MyDistributedEvent");
        _topics.Add(typeof(UpdateProductCommand), "UpdateProductCommand");
        _topics.Add(typeof(ProductUpdatedEvent), "ProductUpdatedEvent");
    }

    public string GetTopic(Type type) => _topics[type];
}
