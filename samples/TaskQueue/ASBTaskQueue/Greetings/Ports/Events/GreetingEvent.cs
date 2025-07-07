using Paramore.Brighter;
using System;

namespace Greetings.Ports.Events
{
    [PublicationTopic("greeting.event")]
    public class GreetingEvent : Event
    {
        public GreetingEvent() : base(Guid.NewGuid().ToString()) { }

        public GreetingEvent(string greeting) : base(Guid.NewGuid().ToString())
        {
            Greeting = greeting;
        }

        public string? Greeting { get; set; }
    }
}
