namespace Paramore.Domain.Common
{
    public class Address
    {
        public string StreetNumber { get; private set; }
        public string Street { get; private set; }
        public string City { get; private set; }
        public string PostalCode { get; private set; }

        public Address(string streetNumber, string street, string city, string postalCode)
        {
            Street = street;
            StreetNumber = streetNumber;
            PostalCode = postalCode;
            City = city;
        }
    }

}