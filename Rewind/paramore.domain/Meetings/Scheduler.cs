using System;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Venues;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Domain.Meetings
{
    public class Scheduler : IScheduler
    {
        private readonly IAmAnOverbookingPolicy _overbookingPolicy;

        public Scheduler(IAmAnOverbookingPolicy _overbookingPolicy)
        {
            this._overbookingPolicy = _overbookingPolicy;
        }

        public Meeting Schedule(Id meetingId, MeetingDate on, Id venue, Id speaker, Capacity capacity)
        {
            if (on == null)
                throw new ArgumentNullException("on", "A meeting must have a date to be scheduled");

            var tickets = _overbookingPolicy.AllocateTickets(capacity);

            var meeting = new Meeting(meetingDate: on, venue: venue, speaker: speaker, tickets: tickets, version: new Version(), id: meetingId);
            meeting.OpenForRegistration();
            return meeting;

        }
    }
}