using System;
using Fohjin.DDD.EventStore.Storage.Memento;

namespace UserGroupManagement.Domain.Momentos
{
    public class MeetingMemento : IMemento
    {
        public DateTime MeetingTime { get; private set; }
        public Guid LocationId { get; private set; }
        public Guid SpeakerId { get; set; }
        public int Capacity { get; set; }
        
        public MeetingMemento(DateTime meetingOn, Guid locationId, Guid speakerId, int capacity)
        {
            MeetingTime = meetingOn;
            LocationId = locationId;
            SpeakerId = speakerId;
            Capacity = capacity;
        }
    }
}