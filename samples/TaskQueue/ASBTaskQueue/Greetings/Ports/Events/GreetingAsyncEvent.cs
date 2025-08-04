using System;
using Paramore.Brighter;

namespace Greetings.Ports.Events
{
    [PublicationTopic("greeting.Asyncevent")]
    public class GreetingAsyncEvent : Event
    {
        public GreetingAsyncEvent() : base(Id.Random()) { }

        public GreetingAsyncEvent(string greeting) : base(Id.Random())
        {
            Greeting = greeting;
        }

        public string? Greeting { get; set; }
    }
}
