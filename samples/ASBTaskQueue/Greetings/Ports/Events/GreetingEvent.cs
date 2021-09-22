using Paramore.Brighter;
using System;

namespace Greetings.Ports.Events
{
    public class GreetingEvent : Event
    {
        public GreetingEvent() : base(Guid.NewGuid()) { }

        public GreetingEvent(string greeting) : base(Guid.NewGuid())
        {
            Greeting = greeting;
        }

        public string Greeting { get; set; }
    }
}
