using Paramore.Domain.Documents;
using Paramore.Domain.ValueTypes;
using Paramore.Infrastructure.Repositories;
using Version = Paramore.Infrastructure.Repositories.Version;

namespace Paramore.Domain.Entities.Meetings
{
    public class Meeting : AggregateRoot<MeetingDocument>
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

        public void OpenForRegistration()
        {
            state = MeetingState.Live;
        }

        #region Aggregate Persistence
        public override void Load(MeetingDocument document)
        {
            id = new Id(document.Id);
            version = new Version(document.Version);
            meetingDate = new MeetingDate(document.MeetingDate);
            speaker = new Id(document.Speaker);
            state = document.State;
            tickets = new Tickets(document.Tickets);
            venue = new Id(document.Venue);
        }

        protected override MeetingDocument ToDocument()
        {
            return new MeetingDocument(
                Id, 
                meetingDate, 
                venue, 
                speaker, 
                tickets.ToDocumentSections(), 
                state, 
                version);
        }
        #endregion

        public override string ToString()
        {
            return string.Format("MeetingDate: {0}, Speaker: {1}, State: {2}, Tickets: {3}, Venue: {4}", meetingDate, speaker, state, tickets, venue);
        }
    }
}