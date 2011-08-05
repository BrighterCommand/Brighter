using System;
using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Speakers
{
    public class SpeakerFactory
    {
        public Speaker Create(Name _name, SpeakerBio speakerBio, PhoneNumber phoneNumber, EmailAddress emailAddress)
        {
            return new Speaker(new Id(), new Version());
        }
    }
}