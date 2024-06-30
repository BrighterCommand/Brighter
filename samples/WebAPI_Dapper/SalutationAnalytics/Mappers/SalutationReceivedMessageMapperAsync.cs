using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.Extensions;
using SalutationApp.Requests;

namespace SalutationAnalytics.Mappers;

public class SalutationReceivedMessageMapperAsync : IAmAMessageMapperAsync<SalutationReceived>
{
    public IRequestContext Context { get; set; }

    public async Task<Message> MapToMessageAsync(SalutationReceived request, Publication publication,
        CancellationToken cancellationToken = default)
    {
        //NOTE: We are showing an async pipeline here, but it is often overkill by comparison to using 
        //TaskCompletionSource for a Task over sync instead
        MessageHeader header = new(request.Id, publication.Topic, request.RequestToMessageType());
        using MemoryStream ms = new();
        await JsonSerializer.SerializeAsync(ms, request, new JsonSerializerOptions(JsonSerializerDefaults.General),
            cancellationToken);
        MessageBody body = new(ms.ToArray());
        return new Message(header, body);
    }

    public Task<SalutationReceived> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
