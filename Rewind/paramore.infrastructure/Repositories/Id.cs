using System;

namespace Paramore.Infrastructure.Repositories
{
    public class Id
    {
        private readonly Guid id;

        public Id(Guid id)
        {
            this.id = id;
        }

        public Id()
        {
            id = Guid.NewGuid();
        }

        public static explicit operator Id(Guid guid)
        {
            return new Id(guid);
        }

        public static implicit operator Guid(Id id)
        {
            return id.id;
        }

        public bool Equals(Id other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.id.Equals(id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Id)) return false;
            return Equals((Id) obj);
        }

        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        public static bool operator ==(Id left, Id right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Id left, Id right)
        {
            return !Equals(left, right);
        }
    }
}