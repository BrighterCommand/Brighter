using System;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Common;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Domain.Speakers
{
    public class SpeakerDocument : IAmADocument
    {
        public SpeakerDocument(Id id, Version version, SpeakerBio bio, PhoneNumber phoneNumber, EmailAddress emailAddress, Name name)
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

        public override string ToString()
        {
            return string.Format("Bio: {0}, Email: {1}, Id: {2}, Name: {3}, PhoneNumber: {4}, Version: {5}", Bio, Email, Id, Name, PhoneNumber, Version);
        }
    }
}