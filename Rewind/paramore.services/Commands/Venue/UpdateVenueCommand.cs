using System;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Venues;
using paramore.commandprocessor;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Adapters.Presentation.API.Handlers
{
    public class UpdateVenueCommand : IRequest
    {
        public UpdateVenueCommand(Guid id, string venueName, string address, string mapURN, string contact, int version)
        {
            Id = new Id(id);
            Address = Address.Parse(address);
            Contact = Contact.Parse(contact);
            VenueMap = new VenueMap(new Uri(mapURN != null ? mapURN : "http://maps.google.co.uk"));
            VenueName = new VenueName(venueName);
            Version = new Version(version);
        }

        public Id Id { get; set; }
        public Address Address { get; set; }
        public Version Version { get; set; }
        public Contact Contact { get; set; }
        public VenueMap VenueMap { get; set; }
        public VenueName VenueName { get; set; }
    }
}