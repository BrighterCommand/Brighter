using System;
using System.Runtime.Serialization;
using Paramore.Adapters.Infrastructure.Repositories;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Domain.Venues
{
    [DataContract]
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

        [DataMember]
        public string Address { get; set; }
        [DataMember]
        public Guid Id { get; set; }
        [DataMember]
        public string VenueName { get; set; }
        [DataMember]
        public string VenueContact { get; set; }
        [DataMember]
        public string VenueMap { get; set; }
        [DataMember]
        public int Version { get; set; }
    }
}