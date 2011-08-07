using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Meetings
{
    public class Meeting : Aggregate<MeetingDTO>  
    {
        private MeetingDate meetingDate;
        private Id venue;
        private Id speaker;
        private Tickets tickets;

        public Meeting(MeetingDate meetingDate, Id venue, Id speaker, Tickets tickets, Version version, Id id)
            :base(id, version)
        {
            this.meetingDate = meetingDate;
            this.venue = venue;
            this.speaker = speaker;
            this.tickets = tickets;
        }

        public override void Load(MeetingDTO dataObject)
        {
            id = new Id(dataObject.Id);
            version = new Version(dataObject.Version);
            meetingDate = new MeetingDate(dataObject.MeetingDate);
            speaker = new Id(dataObject.Speaker);
            tickets = new Tickets(dataObject.Tickets);
            venue = new Id(dataObject.Venue);
        }

        public override MeetingDTO ToDTO()
        {
            return new MeetingDTO(Id, meetingDate, venue, speaker, tickets.ToDTO(), version);
        }
    }

    public class MeetingDTO : IAmADataObject
    {
        public MeetingDTO(Id meetingId, MeetingDate meeting, Id venue, Id speaker, IEnumerable<TicketDTO> tickets, Version version)
        {
            Id = (Guid) meetingId; 
            Venue = (Guid) venue;
            MeetingDate = (DateTime) meeting;
            Speaker = (Guid) speaker;
            Tickets = tickets.ToList();
            Version = (int) version;
        }

        public Guid Id { get; set; }
        public Guid Venue { get; set; }
        public DateTime MeetingDate { get; set; }
        public Guid Speaker { get; set; }
        public List<TicketDTO> Tickets { get; set; }
        public int Version { get; set; }
    }
}