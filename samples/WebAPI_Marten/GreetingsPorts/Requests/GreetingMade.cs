using System;
using Paramore.Brighter;

namespace GreetingsPorts.Requests
{
    public class GreetingMade : Event
    {
        public string Greeting { get; }

        public GreetingMade(string greeting) : base(Guid.NewGuid())
            => Greeting = greeting;
    }
}
