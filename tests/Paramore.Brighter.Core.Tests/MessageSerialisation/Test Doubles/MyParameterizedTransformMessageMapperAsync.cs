using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyParameterizedTransformMessageMapperAsync: IAmAMessageMapperAsync<MyTransformableCommand>
{
    public IRequestContext Context { get; set; }

    [MyParameterizedWrapWith(0,  displayFormat: "I am a format indicator {0}" )]
    public Task<Message> MapToMessageAsync(MyTransformableCommand request, Publication publication, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<Message>();
        tcs.SetResult(new Message(
            new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.General)))
        ));
        return tcs.Task;
    }                                                       

    [MyParameterizedUnwrapWith(0, template: "I am a parameterized template: {0}")]
    public Task<MyTransformableCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<MyTransformableCommand>();
        tcs.SetResult(JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value));
        return tcs.Task;
    }
}
