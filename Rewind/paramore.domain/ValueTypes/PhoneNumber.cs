using System;

namespace Paramore.Domain.ValueTypes
{
    public class PhoneNumber : IEquatable<PhoneNumber>, IAmAValueType<string>
    {
        private readonly string number = string.Empty;

        public PhoneNumber(string number)
        {
            this.number = number;
        }

        public PhoneNumber() {}

        public string Value
        {
            get { return number; }
        }

        public static implicit operator string(PhoneNumber rhs)
        {
            return rhs.number;
        }

        public override string ToString()
        {
            return string.Format("{0}", number);
        }

        public bool Equals(PhoneNumber rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.number, number);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (PhoneNumber)) return false;
            return Equals((PhoneNumber) rhs);
        }

        public override int GetHashCode()
        {
            return (number != null ? number.GetHashCode() : 0);
        }

        public static bool operator ==(PhoneNumber left, PhoneNumber right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(PhoneNumber left, PhoneNumber right)
        {
            return !Equals(left, right);
        }

    }
}