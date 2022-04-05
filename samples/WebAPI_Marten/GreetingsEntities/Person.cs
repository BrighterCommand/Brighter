using System.Collections.Generic;

namespace GreetingsEntities
{
    public class Person
    {
        public int Id { get; set; }
        public ICollection<Greeting> Greetings { get; set; }
        public string Name { get; set; }

        public Person()
        { }

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
            Greetings.Add(greeting);
        }
    }
}
