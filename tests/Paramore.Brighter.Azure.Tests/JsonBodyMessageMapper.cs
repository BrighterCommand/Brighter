using System.Text.Json;

namespace Paramore.Brighter.Azure.TestDoubles.Tests;

public class JsonBodyMessageMapper<T> : IAmAMessageMapper<T> where T : class, IRequest
{
    private readonly Dictionary<string, string> _topicDirectory;

    public JsonBodyMessageMapper(Dictionary<string, string> topicDirectory)
    {
        _topicDirectory = topicDirectory;
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

        if (!_topicDirectory.ContainsKey(typeof(T).Name))
        {
            throw new Exception($"This message mapper has no knowledge of where to send {typeof(T).Name}");
        }

        var topicName = _topicDirectory[typeof(T).Name];
            
        var header = new MessageHeader(messageId: request.Id, topic: topicName, messageType: messageType);
        var body = new MessageBody(JsonSerializer.Serialize(request));
        var message = new Message(header, body);
        return message;
    }

    public T MapToRequest(Message message)
    {
#pragma warning disable CS8603 // Possible null reference return.
        return JsonSerializer.Deserialize<T>(message.Body.Value, JsonSerialisationOptions.Options);
#pragma warning restore CS8603 // Possible null reference return.
    }
}
