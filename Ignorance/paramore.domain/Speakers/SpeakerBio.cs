namespace Paramore.Domain.Speakers
{
    public class SpeakerBio
    {
        private string bio; 

        public SpeakerBio(string biography)
        {
            this.bio = biography;
        }

        public static implicit operator string(SpeakerBio speakerBio)
        {
            return speakerBio.bio;
        }
    }
}