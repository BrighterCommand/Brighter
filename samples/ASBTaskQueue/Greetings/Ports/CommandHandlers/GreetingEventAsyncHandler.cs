using System;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Ports.Events;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class GreetingEventAsyncHandler : RequestHandlerAsync<GreetingAsyncEvent>
    {
        private readonly InstanceCount _counter;

        public GreetingEventAsyncHandler(InstanceCount counter)
        {
            _counter = counter;
        }

        [Monitoring(step: 1, timing: HandlerTiming.Before)]
        public override async Task<GreetingAsyncEvent> HandleAsync(GreetingAsyncEvent @event, CancellationToken cancellationToken)
        {
            Console.WriteLine("Received Greeting. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine($"Greeting:{@event.Greeting} - Id:{@event.Id} Count:{++_counter.Value} ");
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");

            return await base.HandleAsync(@event);
        }
    }
}
