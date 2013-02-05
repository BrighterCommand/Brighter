using System;
using Paramore.Adapters.Infrastructure.Repositories;
using Paramore.Domain.Common;
using Version = Paramore.Adapters.Infrastructure.Repositories.Version;

namespace Paramore.Domain.Venues
{
    public class Venue : AggregateRoot<VenueDocument> 
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

        #region Aggregate  Persistence
        public override void Load(VenueDocument document)
        {
            address = Address.Parse(document.Address); 
            contact = VenueContact.Parse(document.VenueContact);
            map = new VenueMap(new Uri(document.VenueMap));
            name = new VenueName(document.VenueName);
        }

        protected override VenueDocument ToDocument()
        {
            return new VenueDocument(id, version, name, address, map, contact);
        }

        public static explicit operator VenueDocument(Venue venue)
        {
            return venue.ToDocument();
        }

        #endregion

        public override string ToString()
        {
            return string.Format("Address: {0}, Contact: {1}, Map: {2}, Name: {3}", address, contact, map, name);
        }
    }
}