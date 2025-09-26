using System.ComponentModel.DataAnnotations.Schema;

namespace SalutationApp.Entities
{
    [Table("Salutation")]
    public class Salutation
    {
        public long Id { get; set; }
        public string Greeting { get; set; }
        public byte[]? TimeStamp { get; set; }
        
        public Salutation(string greeting)
        {
            Greeting = greeting;
        }

        public Salutation(int id, string greeting)
        {
            Id = id;
            Greeting = greeting;
        }

    }
}

