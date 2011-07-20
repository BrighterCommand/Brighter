using System;
using UserGroupManagement.Infrastructure.Domain;

namespace UserGroupManagement.Domain.Meetings
{
    public class Meeting : IAggregateRoot  
    {
        private DateTime meetingTime;
        private Guid locationId;
        private Guid speakerId;
        private int capacity;

        private Guid _id;

        private int _version;

        public Meeting(Guid meetingId, DateTime on, Guid locationId, Guid speakerId, int capacity)
            :this()
        {
        }

        public Meeting()
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

        //private void OnMeetingBeingScheduled(MeetingScheduledEvent meetingScheduledEvent)
        //{
        //    Id = meetingScheduledEvent.MeetingId;
        //    meetingTime = meetingScheduledEvent.MeetingTime;
        //    locationId = meetingScheduledEvent.LocationId;
        //    speakerId = meetingScheduledEvent.SpeakerId;
        //    capacity = meetingScheduledEvent.Capacity;
        //}
    }
}