using System;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Common;
using Version = Paramore.Rewind.Core.Adapters.Repositories.Version;

namespace Paramore.Rewind.Core.Domain.Speakers
{
    public class SpeakerDocument : IAmADocument
    {
        public SpeakerDocument(Id id, Version version, SpeakerBio bio, PhoneNumber phoneNumber, EmailAddress emailAddress, SpeakerName name)
        {
            Bio = bio;
            Email = emailAddress;
            Id = id;
            PhoneNumber = phoneNumber;
            Name = name;
            Version = version;
        }

        public SpeakerDocument()
        {
        }

        public string Bio { get; set; }
        public string Email { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public int Version { get; set; }

        public override string ToString()
        {
            return string.Format("Bio: {0}, Email: {1}, Id: {2}, Name: {3}, PhoneNumber: {4}, Version: {5}", Bio, Email, Id, Name, PhoneNumber, Version);
        }
    }
}