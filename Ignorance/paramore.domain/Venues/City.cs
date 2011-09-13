namespace Paramore.Domain.Venues
{
    public class City
    {
        private readonly string name = string.Empty;

        public City(string name)
        {
            this.name = name;
        }

        public City() {}

        public static implicit operator string(City city)
        {
            return city.name;
        }
    }
}