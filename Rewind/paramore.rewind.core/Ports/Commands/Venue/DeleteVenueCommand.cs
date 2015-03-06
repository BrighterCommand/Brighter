using System;
using paramore.commandprocessor;

namespace Paramore.Rewind.Core.Ports.Commands.Venue
{
    public class DeleteVenueCommand : IRequest
    {
        public DeleteVenueCommand(Guid id)
        {
            Id = id;
        }

        public Guid Id { get; set; }
    }
}