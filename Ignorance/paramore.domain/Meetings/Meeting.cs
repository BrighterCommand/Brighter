using System;
using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Meetings
{
    public class Meeting : Aggregate  
    {
        private MeetingDate meeting;
        private Id location;
        private Id speaker;
        private Tickets tickets;

        public Meeting(MeetingDate meeting, Id location, Id speaker, Tickets tickets, Version version, Id id)
            :base(id, version)
        {
            this.meeting = meeting;
            this.location = location;
            this.speaker = speaker;
            this.tickets = tickets;
        }
    }
}