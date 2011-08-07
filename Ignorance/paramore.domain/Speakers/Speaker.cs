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
        private EmailAddress emailAddress;
        private Name name;
        private PhoneNumber phoneNumber;

        public Speaker(Id id, Version version, SpeakerBio bio, PhoneNumber phoneNumber, EmailAddress emailAddress, Name name) : base(id, version)
        {
            this.bio = bio;
            this.emailAddress = emailAddress;
            this.name = name;
            this.phoneNumber = phoneNumber;
        }

        public Speaker() : base(new Id(), new Version()){}

        public override void Load(SpeakerDTO dataObject)
        {
            bio = new SpeakerBio(dataObject.Bio);
            emailAddress = new EmailAddress(dataObject.Email);
            name = new Name(dataObject.Name);
            phoneNumber = new PhoneNumber(dataObject.PhoneNumber);
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
            Bio = (string) bio;
            Email = (string) emailAddress;
            Id = (Guid) id;
            PhoneNumber = (string) phoneNumber;
            Name = (string) name;
            Version = (int)version;
        }

        public string Bio { get; set; }
        public string Email { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public int Version { get; set; }
    }
}