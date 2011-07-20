namespace Paramore.Domain.Speakers
{
    public class SpeakerBio
    {
        public string Biography { get; private set; }

        public SpeakerBio(string biography)
        {
            this.Biography = biography;
        }
    }
}