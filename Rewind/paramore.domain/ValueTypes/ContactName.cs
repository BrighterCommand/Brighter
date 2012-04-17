using System;

namespace Paramore.Domain.ValueTypes
{
    public class ContactName : IEquatable<ContactName>, IAmAValueType<string>
    {
        private readonly string name = string.Empty;

        public ContactName(string name)
        {
            this.name = name;
        }

        public ContactName() {}

        public string Value
        {
            get { return name; }
        }


        public override string ToString()
        {
            return string.Format("{0}", name);
        }

        public bool Equals(ContactName rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.name, name);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (ContactName)) return false;
            return Equals((ContactName) rhs);
        }

        public override int GetHashCode()
        {
            return (name != null ? name.GetHashCode() : 0);
        }

        public static bool operator ==(ContactName left, ContactName right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ContactName left, ContactName right)
        {
            return !Equals(left, right);
        }
    }
}