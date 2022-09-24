namespace GreetingsPorts.Responses
{
    public class FindPersonGreetingsResult
    {
        public string PersonName { get; set; }
        public IEnumerable<Salutation> Greetings { get; set; }
    }

    public class Salutation
    {
        public string Words { get; set; }

        public Salutation(string words)
        {
            Words = words;
        }

    }
}
