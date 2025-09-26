using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace GreetingsApp.Entities
{
    [Table("Person")]
    public class Person
    {
        public int Id { get; set; }
        public string Name { get; }
        public byte[] TimeStamp { get; set; }
        public ICollection<Greeting> Greetings { get; set; } = new List<Greeting>();

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
            Greetings.Add(greeting);
        }
    }
}
