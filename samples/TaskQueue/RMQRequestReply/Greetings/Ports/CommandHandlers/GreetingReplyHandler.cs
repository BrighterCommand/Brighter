using System;
using Greetings.Ports.Commands;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class GreetingReplyHandler : RequestHandler<GreetingReply>
    {
        public override GreetingReply Handle(GreetingReply advanceTimerEvent)
        {
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Received Greeting. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine(advanceTimerEvent.Salutation);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");
            Console.ResetColor();
            return base.Handle(advanceTimerEvent);

        }
    }
}
