using System;
using Paramore.Domain.Common;

namespace Paramore.Domain.Speakers
{
    public class SpeakerName : IEquatable<SpeakerName>, IAmAValueType<string>
    {
        private readonly string fullname; 

        public SpeakerName(string name)
        {
            this.fullname = name;
        }

        public static implicit operator string(SpeakerName rhs)
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

        public bool Equals(SpeakerName rhs)
        {
            if (ReferenceEquals(null, rhs)) return false;
            if (ReferenceEquals(this, rhs)) return true;
            return Equals(rhs.fullname, fullname);
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