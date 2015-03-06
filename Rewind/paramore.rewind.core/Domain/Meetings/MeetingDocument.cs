using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Rewind.Core.Adapters.Repositories;
using Version = Paramore.Rewind.Core.Adapters.Repositories.Version;

namespace Paramore.Rewind.Core.Domain.Meetings
{
    public class MeetingDocument : IAmADocument
    {
        public MeetingDocument(Id meetingId, MeetingDate meeting, Id venue, Id speaker, IEnumerable<MeetingDocumentTickets> tickets, MeetingState state,
                               Version version)
        {
            Id = meetingId;
            Venue = venue ?? Guid.Empty;
            MeetingDate = meeting;
            Speaker = speaker ?? Guid.Empty;
            State = MeetingState.Live;
            Tickets = tickets.ToList();
            Version = version;
        }

        public MeetingDocument()
        {
        }

        public Guid Id { get; set; }
        public Guid Venue { get; set; }
        public DateTime MeetingDate { get; set; }
        public Guid Speaker { get; set; }
        public MeetingState State { get; set; }
        public List<MeetingDocumentTickets> Tickets { get; set; }
        public int Version { get; set; }

        public override string ToString()
        {
            return string.Format("Id: {0}, Venue: {1}, MeetingDate: {2}, Speaker: {3}, State: {4}, Tickets: {5}, Version: {6}", Id, Venue, MeetingDate, Speaker,
                                 State, Tickets, Version);
        }
    }
}