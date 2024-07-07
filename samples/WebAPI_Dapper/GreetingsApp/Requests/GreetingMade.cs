using System;
using Paramore.Brighter;

namespace GreetingsApp.Requests;

public class GreetingMade : Event
{
    public GreetingMade(string greeting) : base(Guid.NewGuid())
    {
        Greeting = greeting;
    }

    public string Greeting { get; set; }
}
