namespace Paramore.Domain.Locations
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