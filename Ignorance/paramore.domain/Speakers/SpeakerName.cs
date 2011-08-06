namespace Paramore.Domain.Speakers
{
    public class Name
    {
        public string Fullname { get; private set;}

        public Name(string name)
        {
            this.Fullname = name;
        }
    }
}