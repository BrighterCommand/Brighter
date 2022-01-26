namespace SalutationEntities
{
    public class Salutation
    {
        private int _id;
        public byte[] TimeStamp { get; set; }
         public string Message { get; }
        public Salutation(string greeting)
        {
            Message = greeting;
        }

    }
}

