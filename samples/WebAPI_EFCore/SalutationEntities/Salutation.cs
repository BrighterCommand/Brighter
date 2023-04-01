namespace SalutationEntities
{
    public class Salutation
    {
        private int _id;
        public byte[] TimeStamp { get; set; }
         public string Greeting { get; }
        public Salutation(string greeting)
        {
            Greeting = greeting;
        }

        public Salutation(int id, string greeting)
        {
            _id = id;
            Greeting = greeting;
        }
    }
}

