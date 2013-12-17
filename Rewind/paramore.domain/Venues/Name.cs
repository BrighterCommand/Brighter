using System;
using Paramore.Domain.Common;

namespace Paramore.Domain.Venues
{
    public class Name : IEquatable<Name>, IAmAValueType<string>
    {
        private readonly string name = string.Empty;

        public Name(string name)
        {
            this.name = name;
        }

        public Name() {}

        public string Value
        {
            get { return name; }
        }


        public override string ToString()
        {
            return string.Format("{0}", name);
        }

        public bool Equals(Name rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.name, name);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (Name)) return false;
            return Equals((Name) rhs);
        }

        public override int GetHashCode()
        {
            return (name != null ? name.GetHashCode() : 0);
        }

        public static bool operator ==(Name left, Name right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Name left, Name right)
        {
            return !Equals(left, right);
        }
    }
}