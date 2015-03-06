using System;
using paramore.commandprocessor;
using Paramore.Rewind.Core.Domain.Venues;
using Version = Paramore.Rewind.Core.Adapters.Repositories.Version;

namespace Paramore.Rewind.Core.Ports.Commands.Venue
{
    public class AddVenueCommand : Command, IRequest
    {
        //Required for serialization
        public AddVenueCommand() : base(Guid.NewGuid())
        {
        }

        public AddVenueCommand(string venueName, string address, string mapURN, string contact) : base(Guid.NewGuid())
        {
            Address = Address.Parse(address);
            Contact = Contact.Parse(contact);
            VenueMap = new VenueMap(new Uri(mapURN ?? "http://maps.google.co.uk"));
            VenueName = new VenueName(venueName);
            Version = new Version(1);
        }

        public Address Address { get; private set; }
        public Version Version { get; private set; }
        public Contact Contact { get; private set; }
        public VenueMap VenueMap { get; private set; }
        public VenueName VenueName { get; private set; }
    }
}