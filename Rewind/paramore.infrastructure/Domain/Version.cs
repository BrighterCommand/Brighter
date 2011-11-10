namespace Paramore.Infrastructure.Domain
{
    public class Version
    {
        private readonly int version;

        public Version(int versionNumber)
        {
            version = versionNumber;
        }

        public Version() { }

        public bool Equals(Version other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.version == version;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Version)) return false;
            return Equals((Version) obj);
        }

        public override int GetHashCode()
        {
            return version.GetHashCode();
        }

        public static Version operator ++(Version preIncrement)
        {
            return new Version(preIncrement + 1);
        }

        public static implicit operator int(Version version)
        {
            return version.version;
        }

        public static explicit operator Version(int versionNumber)
        {
            return new Version(versionNumber: versionNumber);
        }

        public static bool operator ==(Version left, Version right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Version left, Version right)
        {
            return !Equals(left, right);
        }
    }
}