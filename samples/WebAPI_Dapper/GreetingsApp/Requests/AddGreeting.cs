using System;
using Paramore.Brighter;

namespace GreetingsPorts.Requests;

public class AddGreeting : Command
{
    public AddGreeting(string name, string greeting)
        : base(Guid.NewGuid())
    {
        Name = name;
        Greeting = greeting;
    }

    public string Name { get; }
    public string Greeting { get; }
}
