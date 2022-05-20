namespace SalutationEntities
{
    public class Salutation
    {
        public int Id { get; }
        public byte[] TimeStamp { get; set; }
        public string Greeting { get; }

        public Salutation() { /* ORM needs to create */ }

        public Salutation(string greeting)
        {
            Greeting = greeting;
        }
    }
}
