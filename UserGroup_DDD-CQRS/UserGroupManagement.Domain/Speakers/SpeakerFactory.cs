using UserGroupManagement.Domain.Common;

namespace UserGroupManagement.Domain.Speakers
{
    public class SpeakerFactory
    {
        public Speaker Create(SpeakerName speakerName, SpeakerBio speakerBio, PhoneNumber phoneNumber, EmailAddress emailAddress)
        {
            return new Speaker(speakerName, speakerBio, phoneNumber, emailAddress);
        }
    }
}