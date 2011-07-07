using System;

namespace UserGroupManagement.Domain.Meetings
{
    public class MeetingFactory
    {
        public Meeting Schedule(Guid meetingId, DateTime on, Guid locationId, Guid speakerId, int capacity)
        {
            return new Meeting(meetingId, on, locationId, speakerId, capacity);
        }
    }
}