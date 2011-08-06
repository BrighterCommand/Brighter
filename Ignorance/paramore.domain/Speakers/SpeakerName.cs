namespace Paramore.Domain.Speakers
{
    public class Name
    {
        private string fullname; 

        public Name(string name)
        {
            this.fullname = name;
        }

        public static implicit operator string(Name name)
        {
            return name.fullname;
        }

    }
}