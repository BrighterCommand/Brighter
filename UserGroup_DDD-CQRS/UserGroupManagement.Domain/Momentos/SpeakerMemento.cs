using System;
using Fohjin.DDD.EventStore.Storage.Memento;

namespace UserGroupManagement.Domain.Momentos
{
    public class SpeakerMemento : IMemento
    {
        internal Guid Id { get; private set; }
        internal int Version { get; private set; }
        internal string SpeakerBio { get; private set; }
        internal string SpeakerName { get; private set; }
        internal string PhoneNumber { get; private set; }
        internal string SpeakerEmail { get; private set; }
 
        public SpeakerMemento(Guid id, int version, string speakerBio, string speakerName, string phoneNumber, string speakerEmail)
        {
            Id = id;
            Version = version;
            SpeakerBio = speakerBio;
            SpeakerName = speakerName;
            PhoneNumber = phoneNumber;
            SpeakerEmail = speakerEmail;
        }
    }
}