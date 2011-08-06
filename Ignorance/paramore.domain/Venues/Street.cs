namespace Paramore.Domain.Venues
{
    public class Street
    {
        private readonly string streetNumber;
        private readonly string street;

        public Street(string streetNumber, string street)
        {
            this.streetNumber = streetNumber;
            this.street = street;
        }

        public Street(string street)
        {
            this.street = street;
        }

        public override string ToString()
        {
            return streetNumber == null ? string.Format("{0} {1}", streetNumber, street) : street;
        }

        public static implicit operator string(Street street)
        {
            return street.ToString();
        }
    }
}