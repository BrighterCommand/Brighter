using System;
using System.Collections.Generic;

namespace GreetingsEntities
{
    public class Person
    {
        public byte[] TimeStamp { get; set; }
        public long Id { get; set; }
        public string Name { get; set; }
        public IList<Greeting> Greetings { get; set; } = new List<Greeting>();

        public Person(){ /*Required for DapperExtensions*/}

        public Person(string name)
        {
            Name = name;
        }

        public Person(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
