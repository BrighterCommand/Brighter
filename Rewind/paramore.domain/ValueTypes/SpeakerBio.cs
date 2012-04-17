namespace Paramore.Domain.ValueTypes
{
    public class SpeakerBio : IAmAValueType<string>
    {
        private readonly string bio; 

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

        public override string ToString()
        {
            return string.Format("{0}", bio);
        }

    }
}