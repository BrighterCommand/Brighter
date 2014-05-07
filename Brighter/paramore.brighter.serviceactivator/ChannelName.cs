namespace paramore.brighter.serviceactivator
{
    public class ChannelName
    {
        private readonly string name;

        public ChannelName(string name)
        {
            this.name = name;
        }

        public string Value
        {
            get { return name; }
        }

        public override string ToString()
        {
            return name;
        }

        public static implicit operator string(ChannelName rhs)
        {
            return rhs.ToString();
        }

        public bool Equals(ChannelName other)
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
            return Equals((ChannelName) obj);
        }

        public override int GetHashCode()
        {
            return (name != null ? name.GetHashCode() : 0);
        }

        public static bool operator ==(ChannelName left, ChannelName right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ChannelName left, ChannelName right)
        {
            return !Equals(left, right);
        }   }
}