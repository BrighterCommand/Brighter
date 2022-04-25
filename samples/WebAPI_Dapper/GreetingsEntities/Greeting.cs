using System;

namespace GreetingsEntities
{
    public class Greeting
    {
        public int Id { get; }
        public string Message { get; set; }
        public Person Recipient { get; set; }

        public Greeting(string message)
        {
            Message = message;
        }

        public Greeting(string message, Person recipient)
        {
            Message = message;
            Recipient = recipient;
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
