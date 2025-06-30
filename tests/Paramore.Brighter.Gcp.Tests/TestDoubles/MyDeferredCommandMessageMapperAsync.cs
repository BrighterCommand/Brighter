using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

internal class MyDeferredCommandMessageMapperAsync : IAmAMessageMapperAsync<MyDeferredCommand>
{
    public IRequestContext? Context { get; set; }
    public Task<Message> MapToMessageAsync(MyDeferredCommand request, Publication publication, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<Message>();
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, partitionKey: request.GroupId,  messageType: MessageType.MT_COMMAND);
        var body = new MessageBody(System.Text.Json.JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)));
        var message = new Message(header, body);
        tcs.SetResult(message);
        return tcs.Task;
    }

    public Task<MyDeferredCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<MyDeferredCommand>();
        var myDeferredCommand = JsonSerializer.Deserialize<MyDeferredCommand>(message.Body.Value, JsonSerialisationOptions.Options);
        tcs.SetResult(myDeferredCommand!);
        return tcs.Task;
    }
}
