using System;
using System.Runtime.Serialization;
using Paramore.Adapters.Infrastructure.Repositories;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Domain.Venues
{
    public class VenueDocument : IAmADocument
    {
        public VenueDocument(Id id, Version version, VenueName venueName, Address address, VenueMap venueMap, Contact contact)
        {
            Id = (Guid)id;
            Version = (int) version;
            VenueName = (string) venueName;
            Address = (string) address;
            VenueMap = (string) venueMap;
            VenueContact = (string) contact;
        }

        public VenueDocument() {}

        public string Address { get; set; }
        public Guid Id { get; set; }
        public string VenueName { get; set; }
        public string VenueContact { get; set; }
        public string VenueMap { get; set; }
        public int Version { get; set; }
    }
}