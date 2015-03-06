using System;

namespace Paramore.Rewind.Core.Domain.Venues
{
    public class VenueName : IEquatable<VenueName>
    {
        private readonly string name;

        public VenueName(string venueName)
        {
            name = venueName;
        }

        public bool Equals(VenueName rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.name, name);
        }

        public static implicit operator string(VenueName rhs)
        {
            return rhs.name;
        }

        public override string ToString()
        {
            return string.Format("{0}", name);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (VenueName)) return false;
            return Equals((VenueName) rhs);
        }

        public override int GetHashCode()
        {
            return (name != null ? name.GetHashCode() : 0);
        }

        public static bool operator ==(VenueName left, VenueName right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(VenueName left, VenueName right)
        {
            return !Equals(left, right);
        }
    }
}