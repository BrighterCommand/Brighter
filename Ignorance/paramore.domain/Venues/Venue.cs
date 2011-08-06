using System;
using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Venues
{
    public class Venue : Aggregate<VenueDTO> 
    {
        private VenueName venueName;
        private Address address;
        private VenueMap map;
        private VenueContact venueContact;

        public Venue(Id id, Version version, VenueName venueName, Address address, VenueMap map, VenueContact venueContact) : base(id, version)
        {
            this.venueName = venueName;
            this.address = address;
            this.map = map;
            this.venueContact = venueContact;
        }

        public override VenueDTO ToDTO()
        {
            throw new System.NotImplementedException();
        }
    }

    public class VenueDTO : IAmADataObject
    {
        public VenueDTO(Id id, Version version, VenueName venueName, Address address, VenueMap venueMap, VenueContact venueContact)
        {
            Id = id;
            Version = version;
            VenueName = venueName;
            Address = address;
            VenueMap = venueMap;
            VenueContact = venueContact;
        }

        public string Address { get; set; }
        public Guid Id { get; set; }
        public string VenueName { get; set; }
        public string VenueContact { get; set; }
        public string VenueMap { get; set; }
        public int Version { get; set; }
    }
}