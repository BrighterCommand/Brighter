using System;

namespace GreetingsApp.Entities
{
    public class Greeting
    {
        private int _id;
        public string Message { get; set; }
        public Person Recipient { get; set; }

        public Greeting(string message)
        {
            Message = message;
        }
        
        public Greeting(int id, string message, Person recipient)
        {
            _id = id;
            Message = message;
            Recipient = recipient;
        }

        public string Greet()
        {
            return $"{Message} {Recipient.Name}!";
        }
    }
}
