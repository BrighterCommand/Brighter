using System;
using Greetings.Ports.Commands;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class FarewellEventHandler : RequestHandler<FarewellEvent>
    {
        public override FarewellEvent Handle(FarewellEvent @event)
        {
            Console.WriteLine("Received Farewell. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine(@event.Farewell);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");
            return base.Handle(@event);
        }
    }
}
