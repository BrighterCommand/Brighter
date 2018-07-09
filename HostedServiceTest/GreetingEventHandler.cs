using System;
using Paramore.Brighter;

namespace HostedServiceTest
{
    public class GreetingEventHandler : RequestHandler<GreetingEvent>
    {
        public override GreetingEvent Handle(GreetingEvent @event)
        {
            Console.WriteLine("Received Greeting. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine(@event.Greeting);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");
            return base.Handle(@event);
        }
    }
}