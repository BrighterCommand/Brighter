using System;
using Paramore.Brighter;

namespace GreetingsPorts.Requests
{
    public class AddGreeting : Command
    {
        public string Name { get; set; }
        public string Greeting { get; set; }

        public AddGreeting(string name, string greeting)
            :base(Guid.NewGuid())
        {
            Name = name;
            Greeting = greeting;
        }

    }
}
