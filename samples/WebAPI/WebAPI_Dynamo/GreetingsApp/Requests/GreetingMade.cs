using System;
using Paramore.Brighter;

namespace GreetingsApp.Requests
{
    public class GreetingMade : Event
    {
        public string Greeting { get; set; }
        
        public GreetingMade(string greeting) : base(Guid.NewGuid().ToString())
        {
            Greeting = greeting;
        }
    }
}
