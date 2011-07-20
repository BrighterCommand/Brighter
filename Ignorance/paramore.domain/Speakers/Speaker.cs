using System;
using Paramore.Infrastructure.Domain;

namespace Paramore.Domain.Speakers
{
    public class Speaker : IAggregateRoot
    {
        //private SpeakerBio bio;
        //private PhoneNumber phoneNumber;
        //private EmailAddress emailAddress;
        //private SpeakerName speakerName;
        private Guid id = Guid.Empty;
        private int version = 0;

        public Guid SisoId
        {
            get { return id; }
        }

        public int Version
        {
            get { return version; }
        }

        public int Lock(int expectedVersion)
        {
            return 0; 
        }
    }
}