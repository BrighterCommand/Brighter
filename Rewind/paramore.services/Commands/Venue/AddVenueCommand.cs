using System;
using paramore.commandprocessor;

namespace Paramore.Ports.Services.Commands.Venue
{
    public class AddVenueCommand : Command, IRequest
    {
        public AddVenueCommand(Guid id) : base(id) {}
        public AddVenueCommand():base(Guid.NewGuid()){}

        public string VenueName { get; set; }
    }
}