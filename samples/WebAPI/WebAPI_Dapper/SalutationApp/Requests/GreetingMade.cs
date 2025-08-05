using System;
using Paramore.Brighter;

namespace SalutationApp.Requests;

public class GreetingMade : Event
{
    public GreetingMade(string greeting) : base(Id.Random())
    {
        Greeting = greeting;
    }

    public string Greeting { get; set; }
}
