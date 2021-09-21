using System;
using GreetingsWatcher.Requests;
using Paramore.Brighter;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace GreetingsWatcher
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
