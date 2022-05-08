using System.Collections.Generic;

namespace GreetingsEntities
{
    public class Person
    {
        public int Id { get; set; }
        public ICollection<Greeting> Greetings { get; set; }
        public string Name { get; set; }

        public Person(string name)
        {
            Name = name;
        }

        public void AddGreeting(Greeting greeting)
        {
            if (Greetings is null)
            {
                // this looks really bad, temporary for rmq setup
                Greetings = new List<Greeting>();
            }

            Greetings.Add(greeting);
        }
    }
}
