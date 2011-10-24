using System;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;

namespace Paramore.Domain.Meetings
{
    public class Ticket : IEntity<TicketDTO> 
    {
        private readonly Id id;

        public Ticket()
        {
            id = new Id(Guid.NewGuid());
        }

        public TicketDTO ToDTO()
        {
            return new TicketDTO(new Guid());
        }

        public Id Id
        {
            get { return id; }
        }

        public override string ToString()
        {
            return string.Format("Id: {0}", id);
        }
    }

    public class TicketDTO : IAmADataObject
    {
        public TicketDTO() {}

        public TicketDTO(Guid id)
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