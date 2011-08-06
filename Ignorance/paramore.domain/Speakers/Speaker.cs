using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Speakers
{
    public class Speaker : Aggregate<SpeakerDTO>
    {
        private SpeakerBio bio;
        private PhoneNumber phoneNumber;
        private EmailAddress emailAddress;
        private Name _name;

        public Speaker(Id id, Version version, SpeakerBio bio, PhoneNumber phoneNumber, EmailAddress emailAddress, Name name) : base(id, version)
        {
            this.bio = bio;
            this.phoneNumber = phoneNumber;
            this.emailAddress = emailAddress;
            _name = name;
        }

        public override SpeakerDTO ToDTO()
        {
            throw new System.NotImplementedException();
        }
    }

    public class SpeakerDTO : IAmADataObject
    {
    }
}