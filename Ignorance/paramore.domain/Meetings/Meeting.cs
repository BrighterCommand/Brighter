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
        private Id speaker;
        private MeetingState state = MeetingState.Draft;
        private Tickets tickets;
        private Id venue;

        public Meeting(MeetingDate meetingDate, Id venue, Id speaker, Tickets tickets, Version version, Id id)
            :base(id, version)
        {
            this.meetingDate = meetingDate;
            this.venue = venue;
            this.speaker = speaker;
            this.tickets = tickets;
        }

        public Meeting() : base(new Id(), new Version()) {}

        public override void Load(MeetingDTO dataObject)
        {
            id = new Id(dataObject.Id);
            version = new Version(dataObject.Version);
            meetingDate = new MeetingDate(dataObject.MeetingDate);
            speaker = new Id(dataObject.Speaker);
            state = dataObject.State;
            tickets = new Tickets(dataObject.Tickets);
            venue = new Id(dataObject.Venue);
        }

        public void OpenForRegistration()
        {
            state = MeetingState.Live;
        }

        public override MeetingDTO ToDTO()
        {
            return new MeetingDTO(Id, meetingDate, venue, speaker, tickets.ToDTO(), state, version);
        }

    }

    public class MeetingDTO : IAmADataObject
    {
        public MeetingDTO(Id meetingId, MeetingDate meeting, Id venue, Id speaker, IEnumerable<TicketDTO> tickets, MeetingState state, Version version)
        {
            Id = (Guid) meetingId; 
            Venue = venue ?? Guid.Empty;
            MeetingDate = (DateTime) meeting;
            Speaker = speaker ?? Guid.Empty;
            State = MeetingState.Live;
            Tickets = tickets.ToList();
            Version = (int) version;
        }

        public MeetingDTO() {}

        public Guid Id { get; set; }
        public Guid Venue { get; set; }
        public DateTime MeetingDate { get; set; }
        public Guid Speaker { get; set; }
        public MeetingState State { get; set; }
        public List<TicketDTO> Tickets { get; set; }
        public int Version { get; set; }
    }
}