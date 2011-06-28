using System;

namespace UserGroupManagement.ServiceLayer.Events.Speaker
{
    public class SpeakerCreatedEvent : DomainEvent
    {
        public Guid SpeakerId { get; private set; }
        public string Name { get; private set; }
        public string Biography { get; private set; }
        public string PhoneNumber { get; private set; }
        public string Email { get; private set; }
        public int Version { get; private set; }
 
        public SpeakerCreatedEvent(Guid speakerId, string name, string biography, string phoneNumber, string email)
        {
            SpeakerId = speakerId;
            Name = name;
            Biography = biography;
            PhoneNumber = phoneNumber;
            Email = email;
        }

    }
}