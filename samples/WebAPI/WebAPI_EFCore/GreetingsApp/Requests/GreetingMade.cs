using System;
using Paramore.Brighter;

namespace GreetingsApp.Requests
{
    public class GreetingMade(string greeting) : Event(Id.Random())
    {
        public string Greeting { get; set; } = greeting;
    }
}
