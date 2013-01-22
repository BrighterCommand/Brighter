using System;
using Paramore.Adapters.Infrastructure.Repositories;

namespace Paramore.Domain.Meetings
{
    public class MeetingDocumentTickets : IAmPartOfADocument
    {
        public MeetingDocumentTickets() {}

        public MeetingDocumentTickets(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; set; }

        public override string ToString()
        {
            return string.Format("Id: {0}", Id);
        }
    }
}