namespace GreetingsEntities
{
    public class Greeting
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public Person Recipient { get; set; }

        public Greeting()
        { }

        public Greeting(string message)
        {
            Message = message;
        }

        public Greeting(int id, string message, Person recipient)
        {
            Id = id;
            Message = message;
            Recipient = recipient;
        }

        public string Greet()
        {
            return $"{Message} {Recipient.Name}!";
        }
    }
}
