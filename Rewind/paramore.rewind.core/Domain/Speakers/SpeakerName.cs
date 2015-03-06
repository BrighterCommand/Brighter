using System;
using Paramore.Rewind.Core.Domain.Common;

namespace Paramore.Rewind.Core.Domain.Speakers
{
    public class SpeakerName : IEquatable<SpeakerName>, IAmAValueType<string>
    {
        private readonly string fullname;

        public SpeakerName(string name)
        {
            fullname = name;
        }

        public string Value
        {
            get { return fullname; }
        }

        public bool Equals(SpeakerName rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.fullname, fullname);
        }

        public static implicit operator string(SpeakerName rhs)
        {
            return rhs.fullname;
        }

        public override string ToString()
        {
            return string.Format("{0}", fullname);
        }

        public override bool Equals(object rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            if (rhs.GetType() != typeof (SpeakerName)) return false;
            return Equals((SpeakerName) rhs);
        }

        public override int GetHashCode()
        {
            return (fullname != null ? fullname.GetHashCode() : 0);
        }

        public static bool operator ==(SpeakerName left, SpeakerName right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SpeakerName left, SpeakerName right)
        {
            return !Equals(left, right);
        }
    }
}