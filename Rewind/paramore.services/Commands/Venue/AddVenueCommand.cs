using System;
using Paramore.Domain.Venues;
using paramore.commandprocessor;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Ports.Services.Commands.Venue
{
    public class AddVenueCommand : Command, IRequest
    {
        //Required for serialization
        public AddVenueCommand() : base(Guid.NewGuid()) {}

        public AddVenueCommand(Guid id, string venueName, string address, string mapURN, string contact) : base(id)
        {
            Address = Address.Parse(address);
            VenueContact = VenueContact.Parse(contact);
            VenueMap = new VenueMap(new Uri(mapURN));
            VenueName = new VenueName(venueName);
            Version = new Version(1);
        }

        public Address Address { get; set; }
        public Version Version { get; set; }
        public VenueContact VenueContact { get; set; }
        public VenueMap VenueMap { get; set; }
        public VenueName VenueName { get; set; }
    }
}