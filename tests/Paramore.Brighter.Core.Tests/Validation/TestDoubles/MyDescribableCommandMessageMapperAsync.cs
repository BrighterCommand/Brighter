using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Core.Tests.Validation.TestDoubles;

/// <summary>
/// An async-only mapper for <see cref="MyDescribableCommand"/> that declares a wrap transform on
/// MapToMessageAsync and an unwrap transform (MyDescribableTransform) on MapToRequestAsync. Used to
/// test that the consumer unwrap-transform check evaluates async-resolved mappers (FR-5), since the
/// sync describe path resolves no mapper when only an async mapper is registered.
/// </summary>
public class MyDescribableCommandMessageMapperAsync : IAmAMessageMapperAsync<MyDescribableCommand>
{
    public IRequestContext? Context { get; set; }

    [MyDescribableWrapWith(0)]
    public Task<Message> MapToMessageAsync(MyDescribableCommand request, Publication publication, CancellationToken cancellationToken = default)
    {
        var message = new Message(
            new MessageHeader(request.Id, publication.Topic, request.RequestToMessageType(), timeStamp: DateTime.UtcNow),
            new MessageBody(JsonSerializer.Serialize(request, JsonSerializerOptions.Default)));
        return Task.FromResult(message);
    }

    [MyDescribableUnwrapWith(0)]
    public Task<MyDescribableCommand> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(JsonSerializer.Deserialize<MyDescribableCommand>(message.Body.Value)!);
    }
}
