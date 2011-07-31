using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Speakers
{
    public class Speaker : Aggregate
    {
        //private SpeakerBio bio;
        //private PhoneNumber phoneNumber;
        //private EmailAddress emailAddress;
        //private SpeakerName speakerName;
        public Speaker(Id id, Version version) : base(id, version)
        {
        }
    }
}