using System;

namespace paramore.brighter.serviceactivator
{
    public class ConnectionName : IEquatable<ConnectionName>
    {
        private readonly string name;

        public ConnectionName(string name)
        {
            this.name = name;
        }

        public override string ToString()
        {
            return name;
        }

        public bool Equals(ConnectionName other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(name, other.name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ConnectionName) obj);
        }

        public override int GetHashCode()
        {
            return (name != null ? name.GetHashCode() : 0);
        }

        public static bool operator ==(ConnectionName left, ConnectionName right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ConnectionName left, ConnectionName right)
        {
            return !Equals(left, right);
        }
    }
}