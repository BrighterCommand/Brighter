using System;
using Fohjin.DDD.EventStore;
using Fohjin.DDD.EventStore.Aggregate;
using Fohjin.DDD.EventStore.Storage.Memento;
using UserGroupManagement.Domain.Common;
using UserGroupManagement.Domain.Momentos;
using UserGroupManagement.Events.Speaker;

namespace UserGroupManagement.Domain.Speakers
{
    public class Speaker : BaseAggregateRoot<IDomainEvent>, IOrginator
    {
        private SpeakerBio speakerBio;
        private PhoneNumber phoneNumber;
        private EmailAddress emailAddress;
        private SpeakerName speakerName;

        public Speaker()
        {
            RegisterEvents();
        }
 
        public Speaker(SpeakerName speakerName, SpeakerBio speakerBio, PhoneNumber phoneNumber, EmailAddress emailAddress)
            : this()
        {
            Apply(new SpeakerCreatedEvent(Guid.NewGuid(), speakerName.Name, speakerBio.Biography, phoneNumber.Number, emailAddress.Email));
        }

 
        public IMemento CreateMemento()
        {
            return new SpeakerMemento(Id, Version, speakerBio.Biography, speakerName.Name, phoneNumber.Number, emailAddress.Email);
        }

        public void SetMemento(IMemento memento)
        {
            var speakerMemento = (SpeakerMemento) memento;
            Id = speakerMemento.Id;
            Version = speakerMemento.Version;
            speakerBio = new SpeakerBio(speakerMemento.SpeakerBio);
            phoneNumber = new PhoneNumber(speakerMemento.PhoneNumber);
            emailAddress = new EmailAddress(speakerMemento.SpeakerEmail);
            speakerName = new SpeakerName(speakerMemento.SpeakerName);
        }

        private void RegisterEvents()
        {
            RegisterEvent<SpeakerCreatedEvent>(OnNewSpeakerCreated);
        }

        private void OnNewSpeakerCreated(SpeakerCreatedEvent speakerCreatedEvent)
        {
            Id = speakerCreatedEvent.Id;
            speakerBio = new SpeakerBio(speakerCreatedEvent.Biography);
            phoneNumber = new PhoneNumber(speakerCreatedEvent.PhoneNumber);
            emailAddress = new EmailAddress(speakerCreatedEvent.Email);
            speakerName = new SpeakerName(speakerCreatedEvent.Name);
        }
    }
}