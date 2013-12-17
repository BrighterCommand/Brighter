using Paramore.Domain.Common;

namespace Paramore.Domain.Speakers
{
    public class SpeakerBio : IAmAValueType<string>
    {
        private readonly string bio;

        public SpeakerBio() {}

        public SpeakerBio(string biography)
        {
            this.bio = biography;
        }

        public string Value
        {
            get { return bio; }
        }

        public static implicit operator string(SpeakerBio rhs)
        {
            return rhs.bio;
        }

        protected bool Equals(SpeakerBio other)
        {
            return string.Equals(bio, other.bio);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SpeakerBio) obj);
        }

        public override int GetHashCode()
        {
            return (bio != null ? bio.GetHashCode() : 0);
        }

        public static bool operator ==(SpeakerBio left, SpeakerBio right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SpeakerBio left, SpeakerBio right)
        {
            return !Equals(left, right);
        }


        public override string ToString()
        {
            return string.Format("{0}", bio);
        }

    }
}