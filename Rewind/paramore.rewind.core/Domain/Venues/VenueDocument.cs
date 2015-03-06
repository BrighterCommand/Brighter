using System;
using Paramore.Rewind.Core.Adapters.Repositories;
using Version = Paramore.Rewind.Core.Adapters.Repositories.Version;

namespace Paramore.Rewind.Core.Domain.Venues
{
    public class VenueDocument : IAmADocument
    {
        public VenueDocument(Id id, Version version, VenueName venueName, Address address, VenueMap venueMap, Contact contact)
        {
            Id = id;
            Version = version;
            VenueName = venueName;
            Address = address;
            VenueMap = venueMap;
            VenueContact = contact;
        }

        public VenueDocument()
        {
        }

        public string Address { get; set; }
        public Guid Id { get; set; }
        public string VenueName { get; set; }
        public string VenueContact { get; set; }
        public string VenueMap { get; set; }
        public int Version { get; set; }
    }
}