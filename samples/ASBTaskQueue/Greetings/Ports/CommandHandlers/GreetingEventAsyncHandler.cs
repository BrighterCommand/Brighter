using System;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Events;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class GreetingEventAsyncHandler : RequestHandlerAsync<GreetingAsyncEvent>
    {
        public override async Task<GreetingAsyncEvent> HandleAsync(GreetingAsyncEvent @event, CancellationToken cancellationToken)
        {
            Console.WriteLine("Received Greeting. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine(@event.Greeting);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");

            return await base.HandleAsync(@event);
        }
    }
}
