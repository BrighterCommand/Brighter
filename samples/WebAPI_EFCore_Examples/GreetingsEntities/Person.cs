using System;
using System.Collections.Generic;

namespace GreetingsEntities
{
    public class Person 
    {
        public Guid Id { get; set; }
        public byte[] TimeStamp { get; set; }
        public string Name { get; private set; }
        public ICollection<Greeting> Greetings { get; set; }

        public Person(string name)
        {
            Name = name;
        }

    }
}
