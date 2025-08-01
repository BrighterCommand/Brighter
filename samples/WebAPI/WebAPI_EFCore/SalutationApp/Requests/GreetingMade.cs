using System;
using Paramore.Brighter;

namespace SalutationApp.Requests
{
    public class GreetingMade(string greeting) : Event(Id.Random())
    {
        public string Greeting { get; init; } = greeting;
    }
}
