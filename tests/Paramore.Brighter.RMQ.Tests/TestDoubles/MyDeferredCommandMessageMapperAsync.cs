using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.RMQ.Tests.TestDoubles;

internal class MyDeferredCommandMessageMapperAsync : IAmAMessageMapperAsync<MyDeferredCommand>
{
    public IRequestContext Context { get; set; }

    public async Task<Message> MapToMessageAsync(MyDeferredCommand request, Publication publication, CancellationToken cancellationToken = default)
    {
        if (publication.Topic is null) throw new InvalidOperationException("Missing publication topic");
            
        var header = new MessageHeader(messageId: request.Id, topic: publication.Topic, messageType: request.RequestToMessageType());
        var body = new MessageBody(await Task.Run(() => JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General))));
        var message = new Message(header, body);
        return message;
    }

    public async Task<MyDeferredCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        var command = await Task.Run(() => JsonSerializer.Deserialize<MyDeferredCommand>(message.Body.Value, JsonSerialisationOptions.Options), cancellationToken);
        return command ?? new MyDeferredCommand();
    }
}
