using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Paramore.Brighter;
using SalutationPorts.Requests;

namespace SalutationAnalytics.Mappers
{
    public class GreetingMadeMessageMapperAsync : IAmAMessageMapperAsync<GreetingMade>
    {
        public Task<Message> MapToMessage(GreetingMade request)
        {
            throw new System.NotImplementedException();
        }

        public async Task<GreetingMade> MapToRequest(Message message)
        {
            //NOTE: We are showing an async pipeline here, but it is often overkill by comparison to using 
            //TaskCompletionSource for a Task over sync instead
            using var ms = new MemoryStream(message.Body.Bytes); 
            return await JsonSerializer.DeserializeAsync<GreetingMade>(ms, JsonSerialisationOptions.Options);
        }
    }
}
