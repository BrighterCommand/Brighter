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
            return string.Format("{0}, {1}, {2}", street, city, postalCode);
        }
    }
}