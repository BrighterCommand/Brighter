﻿using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using SalutationPorts.Requests;

namespace SalutationAnalytics.Mappers
{
    public class GreetingMadeMessageMapperAsync : IAmAMessageMapperAsync<GreetingMade>
    {
        public Task<Message> MapToMessageAsync(GreetingMade request, Publication publication, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public async Task<GreetingMade> MapToRequestAsync(Message message, CancellationToken cancellationToken = default)
        {
            //NOTE: We are showing an async pipeline here, but it is often overkill by comparison to using 
            //TaskCompletionSource for a Task over sync instead
            using var ms = new MemoryStream(message.Body.Bytes); 
            return await JsonSerializer.DeserializeAsync<GreetingMade>(ms, JsonSerialisationOptions.Options, cancellationToken);
        }
    }
}
