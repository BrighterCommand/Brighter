using System;
using GreetingsPorts.Requests;
using Paramore.Brighter;

namespace GreetingsPorts.Handlers
{
    public class GreetingMadeHandlerAsync : RequestHandler<GreetingMade>
    {
        public override GreetingMade Handle(GreetingMade @event)
        {
            Console.WriteLine(@event.Greeting);
            return base.Handle(@event);
        }
    }
}
