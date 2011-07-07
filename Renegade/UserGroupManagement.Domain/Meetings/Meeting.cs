using System;
using Fohjin.DDD.EventStore;
using Fohjin.DDD.EventStore.Aggregate;
using Fohjin.DDD.EventStore.Storage.Memento;
using UserGroupManagement.Domain.Momentos;
using UserGroupManagement.Events.Meeting;

namespace UserGroupManagement.Domain.Meetings
{
    public class Meeting : BaseAggregateRoot<IDomainEvent>, IOrginator, IEquatable<Meeting>
    {
        private DateTime meetingTime;
        private Guid locationId;
        private Guid speakerId;
        private int capacity;

        public Meeting(Guid meetingId, DateTime on, Guid locationId, Guid speakerId, int capacity)
            :this()
        {
            Apply(new MeetingScheduledEvent(meetingId, on, locationId, speakerId, capacity));
        }

        public Meeting()
        {
            RegisterEvents();
        }

  
        public IMemento CreateMemento()
        {
            return new MeetingMemento(meetingTime, locationId, speakerId, capacity);
        }

        public void SetMemento(IMemento memento)
        {
            var meetingMemento = (MeetingMemento) memento;
            meetingTime = meetingMemento.MeetingTime;
            locationId = meetingMemento.LocationId;
            speakerId = meetingMemento.SpeakerId;
            capacity = meetingMemento.Capacity;
        }   
        
        private void RegisterEvents()
        {
            RegisterEvent<MeetingScheduledEvent>(OnMeetingBeingScheduled);
        }

        private void OnMeetingBeingScheduled(MeetingScheduledEvent meetingScheduledEvent)
        {
            Id = meetingScheduledEvent.MeetingId;
            meetingTime = meetingScheduledEvent.MeetingTime;
            locationId = meetingScheduledEvent.LocationId;
            speakerId = meetingScheduledEvent.SpeakerId;
            capacity = meetingScheduledEvent.Capacity;
        }

        public bool Equals(Meeting other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return (other.Id.Equals(Id) && other.meetingTime.Equals(meetingTime) && other.locationId.Equals(locationId) && other.speakerId.Equals(speakerId) && other.capacity == capacity);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (Meeting)) return false;
            return Equals((Meeting) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = meetingTime.GetHashCode();
                result = (result*397) ^ Id.GetHashCode();
                result = (result*397) ^ locationId.GetHashCode();
                result = (result*397) ^ speakerId.GetHashCode();
                result = (result*397) ^ capacity;
                return result;
            }
        }

        public static bool operator ==(Meeting left, Meeting right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Meeting left, Meeting right)
        {
            return !Equals(left, right);
        }
    }
}