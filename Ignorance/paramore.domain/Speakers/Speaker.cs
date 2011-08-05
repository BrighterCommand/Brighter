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
        public Speaker(Id id, Name name, SpeakerBio bio, Version version) : base(id, version)
        {
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