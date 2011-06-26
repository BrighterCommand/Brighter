using System;

namespace UserGroupManagement.Reporting.Dto
{
    public class MeetingDetailsReport
    {
        public Guid Id { get; private set; }
        public string MeetingTime { get; private set; }
        public int Capacity { get; private set; }
        public Guid LocationId { get; private set; }
        public Guid SpeakerId { get; private set; }

        public MeetingDetailsReport(Guid newGuid, string meetingTime, int capacity, Guid locationId, Guid speakerId)
        {
            Id = newGuid;
            MeetingTime = meetingTime;
            Capacity = capacity;
            LocationId = locationId;
            SpeakerId = speakerId;
        }
    }
}