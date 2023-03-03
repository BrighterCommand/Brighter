using System.Collections.Generic;

namespace GreetingsPorts.Responses
{
    public class FindPersonsGreetings
    {
        public string Name { get; set; }
        public List<Salutation> Greetings { get;set; }

    }

    public class Salutation
    {
        public string Words { get; set; }

        public Salutation() { }

        public Salutation(string words)
        {
            Words = words;
        }

    }
}
