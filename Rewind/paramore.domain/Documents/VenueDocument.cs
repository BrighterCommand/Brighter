using System;
using Paramore.Domain.ValueTypes;
using Paramore.Infrastructure.Repositories;
using Version = Paramore.Infrastructure.Repositories.Version;

namespace Paramore.Domain.Documents
{
    public class VenueDocument : IAmADocument
    {
        public VenueDocument(Id id, Version version, VenueName venueName, Address address, VenueMap venueMap, VenueContact venueContact)
        {
            Id = (Guid)id;
            Version = (int) version;
            VenueName = (string) venueName;
            Address = (string) address;
            VenueMap = (string) venueMap;
            VenueContact = (string) venueContact;
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