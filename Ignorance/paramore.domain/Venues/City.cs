namespace Paramore.Domain.Venues
{
    public class City
    {
        private readonly string name;

        public City(string name)
        {
            this.name = name;
        }

        public static implicit operator string(City city)
        {
            return city.name;
        }
    }
}