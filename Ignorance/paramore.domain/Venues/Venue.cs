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
        private VenueContact contact;

        public Venue(Id id, Version version, VenueName venueName, Address address, VenueMap map, VenueContact contact) : base(id, version)
        {
            this.venueName = venueName;
            this.address = address;
            this.map = map;
            this.contact = contact;
        }

        public override void Load(VenueDTO dataObject)
        {
        }

        public override VenueDTO ToDTO()
        {
            return new VenueDTO(id, version, venueName, address, map, contact);
        }
    }

    public class VenueDTO : IAmADataObject
    {
        public VenueDTO(Id id, Version version, VenueName venueName, Address address, VenueMap venueMap, VenueContact venueContact)
        {
            Id = (Guid)id;
            Version = (int) version;
            VenueName = (string) venueName;
            Address = (string) address;
            VenueMap = (string) venueMap;
            VenueContact = (string) venueContact;
        }

        public string Address { get; set; }
        public Guid Id { get; set; }
        public string VenueName { get; set; }
        public string VenueContact { get; set; }
        public string VenueMap { get; set; }
        public int Version { get; set; }
    }
}