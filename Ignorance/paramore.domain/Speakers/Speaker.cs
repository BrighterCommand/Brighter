using System;
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
        private Name name;

        public Speaker(Id id, Version version, SpeakerBio bio, PhoneNumber phoneNumber, EmailAddress emailAddress, Name name) : base(id, version)
        {
            this.bio = bio;
            this.phoneNumber = phoneNumber;
            this.emailAddress = emailAddress;
            this.name = name;
        }

        public override SpeakerDTO ToDTO()
        {
            return new SpeakerDTO(Id, Version, bio, phoneNumber, emailAddress, name);
        }
    }

    public class SpeakerDTO : IAmADataObject
    {
        public SpeakerDTO(Id id, Version version, SpeakerBio bio, PhoneNumber phoneNumber, EmailAddress emailAddress, Name name)
        {
            Id = (Guid) id;
            Version = (int)version;
            Bio = (string) bio;
            PhoneNumber = (string) phoneNumber;
            Name = (string) name;
        }

        public string Bio { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public int Version { get; set; }
    }
}