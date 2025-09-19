using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace GreetingsApp.Entities
{
    [Table("Greeting")]
    public class Greeting
    {
        public long Id { get; set; }
        public string Message { get; set; }
        public int RecipientId { get; set; }
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
