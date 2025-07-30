using System;
using Paramore.Brighter;

namespace GreetingsApp.Requests;

public class AddGreeting(string name, string greeting) : Command(Id.Random())
{
    public string Name { get; } = name;
    public string Greeting { get; } = greeting;
}
