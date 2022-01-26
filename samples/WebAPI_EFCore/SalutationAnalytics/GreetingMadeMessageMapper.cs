using System;
using Paramore.Brighter;
using SalutationPorts.Requests;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SalutationAnalytics
{
    public class GreetingMadeMessageMapper : IAmAMessageMapper<GreetingMade>
    {
        public Message MapToMessage(GreetingMade request)
        {
            throw new NotImplementedException();
        }

        public GreetingMade MapToRequest(Message message)
        {
            return JsonSerializer.Deserialize<GreetingMade>(message.Body.Value, JsonSerialisationOptions.Options);
        }
    }
}
