namespace Paramore.Domain.Meetings
{
    public class Capacity
    {
        private readonly int capacity;

        public Capacity(int capacity)
        {
            this.capacity = capacity;
        }

        public bool Equals(Capacity other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.capacity == capacity;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Capacity)) return false;
            return Equals((Capacity) obj);
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
    }
}