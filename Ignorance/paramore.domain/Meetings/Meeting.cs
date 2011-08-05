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
        private Id location;
        private Id speaker;
        private Tickets tickets;

        public Meeting(MeetingDate meetingDate, Id location, Id speaker, Tickets tickets, Version version, Id id)
            :base(id, version)
        {
            this.meetingDate = meetingDate;
            this.location = location;
            this.speaker = speaker;
            this.tickets = tickets;
        }

        public Meeting(MeetingDTO dto)
            :base(new Id(dto.Id), new Version(dto.Version))
        {
            meetingDate = new MeetingDate(dto.Meeting);
            location = new Id(dto.Location);
            speaker = new Id(dto.Speaker);
            tickets = new Tickets(dto.Tickets);
        }

        public override MeetingDTO ToDTO()
        {
            return new MeetingDTO(Id, meetingDate, location, speaker, tickets.ToDTO(), version);
        }
    }

    public class MeetingDTO : IAmADataObject
    {
        public MeetingDTO(Id meetingId, MeetingDate meeting, Id location, Id speaker, IEnumerable<TicketDTO> tickets, Version version)
        {
            Id = (Guid) meetingId; 
            Location = (Guid) location;
            Meeting = (DateTime) meeting;
            Speaker = (Guid) speaker;
            Tickets = tickets.ToList();
            Version = (int) version;
        }

        public Guid Id { get; set; }
        public Guid Location { get; set; }
        public DateTime Meeting { get; set; }
        public Guid Speaker { get; set; }
        public List<TicketDTO> Tickets { get; set; }
        public int Version { get; set; }
    }
}