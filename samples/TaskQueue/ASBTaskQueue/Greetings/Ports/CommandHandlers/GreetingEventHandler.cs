using System;
using Greetings.Ports.Events;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class GreetingEventHandler : RequestHandler<GreetingEvent>
    {
        private IAmACommandProcessor _commandProcessor;

        public GreetingEventHandler(IAmACommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
        }

        public override GreetingEvent Handle(GreetingEvent @event)
        {
            Console.WriteLine("Received Greeting. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine(@event.Greeting);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");

            _commandProcessor.Post(new GreetingAsyncEvent("Greetings from Non Async"));

            return base.Handle(@event);
        }
    }
}
