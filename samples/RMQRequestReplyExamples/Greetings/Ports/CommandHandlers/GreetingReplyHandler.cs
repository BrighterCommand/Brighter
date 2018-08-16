using System;
using Greetings.Ports.Commands;
using Paramore.Brighter;

namespace Greetings.Ports.CommandHandlers
{
    public class GreetingReplyHandler : RequestHandler<GreetingReply>
    {
        public override GreetingReply Handle(GreetingReply request)
        {
            Console.WriteLine("Received Greeting. Message Follows");
            Console.WriteLine("----------------------------------");
            Console.WriteLine(request.Salutation);
            Console.WriteLine("----------------------------------");
            Console.WriteLine("Message Ends");

            return base.Handle(request);

        }
    }
}
