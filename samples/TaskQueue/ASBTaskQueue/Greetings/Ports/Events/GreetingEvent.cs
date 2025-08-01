using Paramore.Brighter;
using System;

namespace Greetings.Ports.Events
{
    [PublicationTopic("greeting.event")]
    public class GreetingEvent : Event
    {
        public GreetingEvent() : base(Id.Random()) { }

        public GreetingEvent(string greeting) : base(Id.Random())
        {
            Greeting = greeting;
        }

        public string? Greeting { get; set; }
    }
}
