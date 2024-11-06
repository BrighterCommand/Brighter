using System;
using Paramore.Brighter;

namespace Greetings.Ports.Events
{
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
