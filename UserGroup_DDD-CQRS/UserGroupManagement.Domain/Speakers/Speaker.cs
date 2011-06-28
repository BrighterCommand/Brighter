using System;
using UserGroupManagement.Domain.Common;
using UserGroupManagement.Infrastructure.Domain;

namespace UserGroupManagement.Domain.Speakers
{
    public class Speaker : IAggregateRoot
    {
        private SpeakerBio speakerBio;
        private PhoneNumber phoneNumber;
        private EmailAddress emailAddress;
        private SpeakerName speakerName;

        private Guid _id;

        private int _version;

        public Speaker()
        {
        }
 
        public Speaker(SpeakerName speakerName, SpeakerBio speakerBio, PhoneNumber phoneNumber, EmailAddress emailAddress)
            : this()
        {
        }

        public Guid SisoId
        {
            get { return _id; }
        }

        public int Version
        {
            get { return _version; }
        }

        public int Lock(int expectedVersion)
        {
            throw new NotImplementedException();
        }

        //private void OnNewSpeakerCreated(SpeakerCreatedEvent speakerCreatedEvent)
        //{
        //    Id = speakerCreatedEvent.Id;
        //    speakerBio = new SpeakerBio(speakerCreatedEvent.Biography);
        //    phoneNumber = new PhoneNumber(speakerCreatedEvent.PhoneNumber);
        //    emailAddress = new EmailAddress(speakerCreatedEvent.Email);
        //    speakerName = new SpeakerName(speakerCreatedEvent.Name);
        //}
    }
}