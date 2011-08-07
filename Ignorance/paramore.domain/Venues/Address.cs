using System.Text.RegularExpressions;

namespace Paramore.Domain.Venues
{
    public class Address
    {
        private readonly City city;
        private readonly PostCode postalCode;
        private readonly Street street;

        public Address(Street street, City city, PostCode postalCode)
        {
            this.street = street;
            this.postalCode = postalCode;
            this.city = city;
        }

        public static implicit operator string(Address address)
        {
            return address.ToString();
        }

        public override string ToString()
        {
            return string.Format("Street : {0}, City : {1}, PostCode : {2}", street, city, postalCode);
        }

        public static Address Parse(string address)
        {
            var rx = new Regex("Street : (.*), City : (.*), PostCode : (.*)");
            var match = rx.Match(address);
            var street = new Street(match.Groups[0].Value);
            var city = new City(match.Groups[1].Value);
            var postcode = new PostCode(match.Groups[2].Value);
            return new Address(street, city, postcode);
        }
    }
}