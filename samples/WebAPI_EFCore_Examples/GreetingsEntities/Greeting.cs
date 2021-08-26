using System;

namespace GreetingsEntities
{
    public class Greeting
    {
        public Guid Id { get; set; }
        public string Message { get; set; }
        public Person Recipient { get; set; }

        public Greeting(string message, Person recipient)
        {
            Id = Guid.NewGuid();
            Message = message;
            Recipient = recipient;
        }

        public string Greet()
        {
            return $"{Message} {Recipient.Name}!";
        }
    }
}
