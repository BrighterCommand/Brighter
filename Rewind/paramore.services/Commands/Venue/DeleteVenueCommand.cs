using System;
using paramore.commandprocessor;

namespace Paramore.Ports.Services.Commands.Venue
{
    public class DeleteVenueCommand : IRequest
    {
        public Guid Id { get; set; }

        public DeleteVenueCommand(Guid id)
        {
            Id = id;
        }
    }
}
