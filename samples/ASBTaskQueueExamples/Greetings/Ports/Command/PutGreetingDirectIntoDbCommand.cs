using System;
using Greetings.Ports.Events;

namespace Greetings.Ports.Command
{
    public class PutGreetingDirectIntoDbCommand : Paramore.Brighter.Command
    {
        public PutGreetingDirectIntoDbCommand(Guid id) : base(id)
        {
            
        }
        
        public GreetingEvent Greeting { get; set; }
    }
}
