using System;
using Paramore.Domain.Common;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Domain.Venues
{
    public class Venue : Aggregate<VenueDTO> 
    {
        private Address address;
        private VenueContact contact;
        private VenueMap map;
        private VenueName name;

        public Venue(Id id, Version version, VenueName name, Address address, VenueMap map, VenueContact contact) : base(id, version)
        {
            this.address = address;
            this.contact = contact;
            this.map = map;
            this.name = name;
        }

        public Venue(Id id, Version version, VenueName venueName) 
            : this(id, version, venueName, new Address(), new VenueMap(), new VenueContact()) {}

        public Venue() : base(new Id(), new Version()){}

        public override void Load(VenueDTO dataObject)
        {
            address = Address.Parse(dataObject.Address); 
            contact = VenueContact.Parse(dataObject.VenueContact);
            map = new VenueMap(new Uri(dataObject.VenueMap));
            name = new VenueName(dataObject.VenueName);
        }

        public override VenueDTO ToDTO()
        {
            return new VenueDTO(id, version, name, address, map, contact);
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