using System;
using Paramore.Domain.Documents;
using Paramore.Infrastructure.Repositories;

namespace Paramore.Domain.Entities.Meetings
{
    public class Ticket : IEntity<MeetingDocumentTickets> 
    {
        private readonly Id id;

        public Ticket()
        {
            id = new Id(Guid.NewGuid());
        }

        public Id Id
        {
            get { return id; }
        }

        #region Persistence
        public MeetingDocumentTickets ToDocumentSection()
        {
            return new MeetingDocumentTickets(new Guid());
        }
        #endregion

        public override string ToString()
        {
            return string.Format("Id: {0}", id);
        }
    }
}