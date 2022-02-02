using System.Text.Json;
using Paramore.Brighter;
using SalutationPorts.Requests;

namespace SalutationAnalytics.Mappers
{
    public class GreetingMadeMessageMapper : IAmAMessageMapper<GreetingMade>
    {
        public Message MapToMessage(GreetingMade request)
        {
            throw new System.NotImplementedException();
        }

        public GreetingMade MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<GreetingMade>(message.Body.Value, JsonSerialisationOptions.Options);
        }
    }
}
