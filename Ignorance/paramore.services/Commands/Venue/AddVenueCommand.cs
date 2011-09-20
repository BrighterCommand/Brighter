using System;
using Paramore.Services.CommandProcessors;
using Paramore.Services.Common;

namespace Paramore.Services.Commands.Venue
{
    public class AddVenueCommand : Command, IRequest
    {
        public AddVenueCommand(Guid id) : base(id) {}
        public AddVenueCommand():base(Guid.NewGuid()){}

        public string VenueName { get; set; }
    }
}