using System;
using Paramore.Brighter;

namespace Greetings.Ports.Events
{
    [PublicationTopic("greeting.Asyncevent")]
    public class GreetingAsyncEvent : Event
    {
        public GreetingAsyncEvent() : base(Guid.NewGuid().ToString()) { }

        public GreetingAsyncEvent(string greeting) : base(Guid.NewGuid().ToString())
        {
            Greeting = greeting;
        }

        public string? Greeting { get; set; }
    }
}
