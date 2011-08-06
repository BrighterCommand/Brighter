namespace Paramore.Domain.Venues
{
    public class ContactName
    {
        public string Name { get; private set; }

        public ContactName(string name)
        {
            Name = name;
        }
    }
}