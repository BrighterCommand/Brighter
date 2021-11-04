using System;
using GreetingsWatcher.Requests;
using Paramore.Brighter;

namespace GreetingsWatcher.Handlers
{
    public class GreetingMadeHandler : RequestHandler<GreetingMade>
    {
        public override GreetingMade Handle(GreetingMade @event)
        {
            Console.WriteLine(@event.Greeting);
            return base.Handle(@event);
        }
    }
}
