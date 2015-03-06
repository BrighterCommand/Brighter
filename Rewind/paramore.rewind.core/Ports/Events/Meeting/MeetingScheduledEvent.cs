using System;
using paramore.commandprocessor;

namespace Paramore.Rewind.Core.Ports.Events.Meeting
{
    public class MeetingScheduledEvent : Event
    {
        public MeetingScheduledEvent(Guid meetingId, DateTime on, Guid locationId, Guid speakerId, int capacity)
        {
            MeetingId = meetingId;
            MeetingTime = on;
            LocationId = locationId;
            SpeakerId = speakerId;
            Capacity = capacity;
        }

        public Guid MeetingId { get; private set; }
        public DateTime MeetingTime { get; private set; }
        public Guid LocationId { get; private set; }
        public Guid SpeakerId { get; private set; }
        public int Capacity { get; private set; }
    }
}