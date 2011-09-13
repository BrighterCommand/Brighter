namespace Paramore.Domain.Venues
{
    public class Street
    {
        private readonly string streetNumber = string.Empty;
        private readonly string street = string.Empty;

        public Street(string streetNumber, string street)
        {
            this.streetNumber = streetNumber;
            this.street = street;
        }

        public Street(string street)
        {
            this.street = street;
        }

        public Street() {}

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