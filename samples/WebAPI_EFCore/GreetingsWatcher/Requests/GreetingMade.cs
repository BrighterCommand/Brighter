using System;
using Paramore.Brighter;

namespace GreetingsWatcher.Requests
{
    public class GreetingMade : Event
    {
        public string Greeting { get; set; }
        
        public GreetingMade(string greeting) : base(Guid.NewGuid())
        {
            Greeting = greeting;
        }
    }
}
