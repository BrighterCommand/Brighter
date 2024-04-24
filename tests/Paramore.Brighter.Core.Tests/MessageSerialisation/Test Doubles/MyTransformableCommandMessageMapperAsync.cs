using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyTransformableCommandMessageMapperAsync : IAmAMessageMapperAsync<MyTransformableCommand>
{
    [MySimpleWrapWith(0)]
    public Task<Message> MapToMessageAsync(MyTransformableCommand request, Publication publication, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<Message>();
        tcs.SetResult(new Message(
            new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
            ));
        return tcs.Task;
    }

    [MySimpleUnwrapWith(0)]
    public Task<MyTransformableCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<MyTransformableCommand>();
        tcs.SetResult(JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value));
        return tcs.Task;
    }
}
