using System;
using Paramore.Domain.Common;

namespace Paramore.Domain.Venues
{
    public class Street : IAmAValueType<string>, IFormattable
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
            return streetNumber != null ? string.Format("StreetNumber: {0}, Street: {1}", streetNumber, street) : string.Format("Street: {0}", street);
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