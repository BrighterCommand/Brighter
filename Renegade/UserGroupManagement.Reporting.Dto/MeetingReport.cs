using System;

namespace UserGroupManagement.Reporting.Dto
{
    public class MeetingReport
    {
        public Guid Id { get; private set; }
        public string MeetingTime { get; private set; }

        public MeetingReport(Guid id, string meetingTime)
        {
            Id = id;
            MeetingTime = meetingTime;
        }
    }
}
