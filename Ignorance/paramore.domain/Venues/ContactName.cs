namespace Paramore.Domain.Venues
{
    public class ContactName
    {
        private readonly string name = string.Empty;

        public ContactName(string name)
        {
            this.name = name;
        }

        public ContactName() {}

        public string Name { get { return name; }}

    }
}