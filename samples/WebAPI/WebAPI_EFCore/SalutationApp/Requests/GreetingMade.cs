using System;
using Paramore.Brighter;

namespace SalutationApp.Requests
{
    public class GreetingMade(string greeting) : Event(Guid.NewGuid().ToString())
    {
        public string Greeting { get; init; } = greeting;
    }
}
