using System;
using Paramore.Domain.Common;

namespace Paramore.Domain.Venues
{
    public class Capacity: IEquatable<Capacity>, IAmAValueType<int>
    {
        private readonly int capacity;

        public Capacity(int capacity)
        {
            this.capacity = capacity;
        }

        public static implicit operator int(Capacity rhs)
        {
            return rhs.capacity;
        }

        public bool Equals(Capacity lhs)
        {
            if (ReferenceEquals(null, lhs)) return false;
            if (ReferenceEquals(this, lhs)) return true;
            return lhs.capacity == capacity;
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (Capacity)) return false;
            return Equals((Capacity) rhs);
        }

        public override int GetHashCode()
        {
            return capacity.GetHashCode();
        }

        public static bool operator ==(Capacity left, Capacity right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Capacity left, Capacity right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return string.Format("{0}", capacity);
        }

        public int Value
        {
            get { return capacity; }
        }
    }
}