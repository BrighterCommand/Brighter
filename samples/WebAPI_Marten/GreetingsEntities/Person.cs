using System.Collections.Generic;

namespace GreetingsEntities
{
    public class Person
    {
        private readonly ICollection<Greeting> _greetings = new List<Greeting>();
        public int Id { get;  set; }
        public int Version { get; set;}
        public string Name { get; set; }
        public IEnumerable<Greeting> Greetings => _greetings;

        public Person(string name)
        {
            Name = name;
        }

        public void AddGreeting(Greeting greeting)
        {
            _greetings.Add(greeting);
        }
    }
}
