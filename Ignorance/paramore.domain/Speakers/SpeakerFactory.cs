using Paramore.Domain.Common;

namespace Paramore.Domain.Speakers
{
    public class SpeakerFactory
    {
        public Speaker Create(SpeakerName speakerName, SpeakerBio speakerBio, PhoneNumber phoneNumber, EmailAddress emailAddress)
        {
            return new Speaker();
        }
    }
}