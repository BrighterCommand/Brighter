using System;
using System.Text.RegularExpressions;
using Paramore.Rewind.Core.Domain.Common;

namespace Paramore.Rewind.Core.Domain.Venues
{
    public class Street : IAmAValueType<string>, IFormattable
    {
        private readonly string street = string.Empty;
        private readonly int streetNumber;

        public Street(int streetNumber, string street)
        {
            this.streetNumber = streetNumber;
            this.street = street;
        }

        public Street(string street)
        {
            this.street = street;
        }

        public Street()
        {
        }

        public string Value
        {
            get { return ToString(); }
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            switch (format)
            {
                case "G":
                default:
                    return ToString();
            }
        }

        public static implicit operator string(Street rhs)
        {
            return rhs.ToString();
        }

        public override string ToString()
        {
            return streetNumber > 0
                ? string.Format("BuidlingNumber: {0}, StreetName: {1}", streetNumber, street)
                : string.Format("BuildingNumber: 0, StreetName: {0}", street);
        }

        public static Street Parse(string street)
        {
            var rx = new Regex("BuildingNumber: (.*), StreetName: (.*)");
            Match match = rx.Match(street);
            int streetNumber = Convert.ToInt32(match.Groups[0].Value);
            string streetName = match.Groups[1].Value;
            return new Street(streetNumber, streetName);
        }
    }
}