using System;
using Paramore.Domain.Common;

namespace Paramore.Domain.Venues
{
    public class City : IEquatable<City>, IAmAValueType<string>
    {
        private readonly string name = string.Empty;

        public City() {}
        
        public City(string name)
        {
            this.name = name;
        }

        public string Value
        {
            get { return ToString(); }
        }

        public static implicit operator string(City rhs)
        {
            return rhs.name;
        }

        public override string ToString()
        {
            return string.Format("{0}", name);
        }

        public bool Equals(City rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.name, name);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (City)) return false;
            return Equals((City) rhs);
        }

        public override int GetHashCode()
        {
            return (name != null ? name.GetHashCode() : 0);
        }

        public static bool operator ==(City left, City right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(City left, City right)
        {
            return !Equals(left, right);
        }
    }
}