using System;
using Greetings.Ports.Commands;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class FarewellEventHandler : RequestHandler<FarewellEvent>
    {
        public override FarewellEvent Handle(FarewellEvent advanceTimerEvent)
        {
            Console.BackgroundColor = ConsoleColor.Blue;
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine("Received Farewell. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine(advanceTimerEvent.Farewell);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");

            Console.ResetColor();
            return base.Handle(advanceTimerEvent);
        }
    }
}
