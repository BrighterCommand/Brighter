using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsWeb.Mappers;

public class GreetingMadeMessageMapperAsync : IAmAMessageMapperAsync<GreetingMade>
{
    public IRequestContext Context { get; set; }

    public async Task<Message> MapToMessageAsync(GreetingMade request, Publication publication,
        CancellationToken cancellationToken = default)
    {
        //NOTE: We are showing an async pipeline here, but it is often overkill by comparison to using 
        //TaskCompletionSource for a Task over sync instead
        //For Kafka Serdes see the Kafka Schema Registry examples
        MessageHeader header = new MessageHeader(request.Id, publication.Topic, MessageType.MT_EVENT);
        using MemoryStream ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, request, new JsonSerializerOptions(JsonSerializerDefaults.General),
            cancellationToken);
        MessageBody body = new MessageBody(ms.ToArray());
        return new Message(header, body);
    }

    public Task<GreetingMade> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
