using System;
using Paramore.Brighter;

namespace HostedServiceTest
{
    public class GreetingEvent : Event
    {
        public GreetingEvent(string greeting) : base(Guid.NewGuid())
        {
            Greeting = greeting;
        }

        public string Greeting { get; set; }
    }
}