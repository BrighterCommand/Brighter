using System;
using System.Collections.Generic;

namespace GreetingsEntities
{
    public class Person
    {
        private readonly List<Greeting> _greetings = new List<Greeting>();
        public byte[] TimeStamp { get; set; }
        public int Id { get; }
        public string Name { get; }
        public IReadOnlyList<Greeting> Greetings => _greetings;

        public Person(string name)
        {
            Name = name;
        }

        public Person(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public void AddGreeting(Greeting greeting)
        {
            greeting.Recipient = this;
            _greetings.Add(greeting);
        }

    }
}
