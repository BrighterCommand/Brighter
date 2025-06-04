using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.JsonConverters;
using SalutationApp.Requests;

namespace SalutationAnalytics.Mappers;

public class GreetingMadeMessageMapperAsync : IAmAMessageMapperAsync<GreetingMade>
{
    public IRequestContext? Context { get; set; }

    public Task<Message> MapToMessageAsync(GreetingMade request, Publication publication,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<GreetingMade> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
    {
        //NOTE: We are showing an async pipeline here, but it is often overkill by comparison to using 
        //TaskCompletionSource for a Task over sync instead
        using MemoryStream ms = new(message.Body.Bytes);
        return await JsonSerializer.DeserializeAsync<GreetingMade>(ms, JsonSerialisationOptions.Options,
            cancellationToken) ?? throw new InvalidOperationException("Failed to deserialize message");
    }
}
