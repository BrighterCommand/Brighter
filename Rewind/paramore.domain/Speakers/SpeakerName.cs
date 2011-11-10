using System;
using Paramore.Domain.Common;

namespace Paramore.Domain.Speakers
{
    public class Name : IEquatable<Name>, IAmAValueType<string>
    {
        private readonly string fullname; 

        public Name(string name)
        {
            this.fullname = name;
        }

        public static implicit operator string(Name rhs)
        {
            return rhs.fullname;
        }

        public string Value
        {
            get { return fullname; }
        }

        public override string ToString()
        {
            return string.Format("{0}", fullname);
        }

        public bool Equals(Name rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.fullname, fullname);
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
            return (fullname != null ? fullname.GetHashCode() : 0);
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