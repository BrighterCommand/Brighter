using System;
using System.Text.RegularExpressions;
using Paramore.Domain.Common;

namespace Paramore.Domain.Venues
{
    public class Street : IAmAValueType<string>, IFormattable
    {
        private readonly int streetNumber;
        private readonly string street = string.Empty;

        public Street(int streetNumber, string street)
        {
            this.streetNumber = streetNumber;
            this.street = street;
        }

        public Street(string street)
        {
            this.street = street;
        }

        public Street() {}

        public string Value
        {
            get { return ToString(); }
        }

        public static implicit operator string(Street rhs)
        {
            return rhs.ToString();
        }

        public override string ToString()
        {
            return streetNumber > 0 ? string.Format("BuidlingNumber: {0}, StreetName: {1}", streetNumber, street) : string.Format("BuildingNumber: 0, StreetName: {0}", street);
        }

        public static Street Parse(string street)
        {
            var rx = new Regex("BuildingNumber: (.*), StreetName: (.*)");
            var match = rx.Match(street);
            var streetNumber = Convert.ToInt32(match.Groups[0].Value);
            var streetName = match.Groups[1].Value;
            return new Street(streetNumber, streetName);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            switch(format)
            {
                case "G":
                default:
                    return ToString();
            }
        }
    }
}